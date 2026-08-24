// Feature: Let Vanilla AI tower rebuilding clear its own blocking tower ruin.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AITowerRuinRepairFix : IDisposable
    {
        private const string BuildingPlacementValidatorPattern =
            "40 53 55 56 57 41 56 48 83 EC 40 33 C0 49 63 E8 83 BC 24 90 00 00 00 02";
        private const int BuildingPlacementValidatorRva = 0x7B060;
        private const int DiagnosticRepeatTicks = 5 * 40;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int BuildingPlacementValidatorDelegate(
            ulong placementStateAddress,
            int tileId,
            int playerId,
            int mapperValue,
            int mode);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<BuildingPlacementValidatorDelegate>> validatorHook =
            new HookRef<X64ManagedFunctionDetourAOB<BuildingPlacementValidatorDelegate>>();
        private readonly Dictionary<DiagnosticKey, int> diagnosticTicks = new Dictionary<DiagnosticKey, int>();
        private bool callbackFailureLogged;
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

            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                BuildingPlacementValidatorPattern,
                BuildingPlacementValidatorRva,
                referenceHashMatches,
                "AI tower-rebuild placement validator",
                log);
            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddDetour(
                    ref validatorHook,
                    libraryBase + unchecked((ulong)resolution.Rva),
                    ValidateBuildingPlacement);
                transaction.Commit();
                if (!validatorHook.Success)
                    throw new InvalidOperationException("The AI tower-rebuild placement-validator hook was not installed.");

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Bugfixes and QoL AI tower-ruin repair hook installed: method={resolution.Method}, rva=0x{resolution.Rva:X}.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
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

            try
            {
                // Player zero is used by candidate-fit probes; requiring a real AI slot limits
                // diagnostics and deletion to live AI tower-placement attempts.
                if (IsEnabled && IsLiveTowerMapper(mapperValue) &&
                    playerId >= 1 && playerId <= 8 &&
                    GamePlayerManagerAPI.Instance.IsAIPlayer(playerId))
                {
                    string outcome = result == 0
                        ? "vanilla-allowed"
                        : InspectBlockedTileAndMaybeMarkRuin(tileId, playerId, mapperValue);
                    LogDiagnostic(playerId, mapperValue, tileId, result, outcome);
                }
            }
            catch (Exception ex)
            {
                if (!callbackFailureLogged)
                {
                    callbackFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"AI tower-ruin repair callback failed; further callback errors are suppressed and Vanilla remains active: {ex}");
                }
            }

            return result;
        }

        private string InspectBlockedTileAndMaybeMarkRuin(int tileId, int playerId, int mapperValue)
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

            UnmanagedVector2<ushort> position = tileApi.GetTileVectorFromId(tileId);
            if (position.X < building->r_TilePositionXBegin || position.X > building->r_TilePositionXEnd ||
                position.Y < building->r_TilePositionYBegin || position.Y > building->r_TilePositionYEnd)
            {
                return $"blocked-ruin-footprint-mismatch:buildingId={buildingId},type={building->r_BuildingType},position=({position.X},{position.Y}),bounds=({building->r_TilePositionXBegin},{building->r_TilePositionYBegin})-({building->r_TilePositionXEnd},{building->r_TilePositionYEnd})";
            }

            eStructs ruinType = building->r_BuildingType;
            uint globalId = building->r_GlobalId;
            if (!buildingApi.DeleteBuildingSafe(buildingId))
                return $"blocked-ruin-delete-rejected:buildingId={buildingId},globalId={globalId},type={ruinType}";

            Shared.DebugLogHelper.LogInfo(
                log,
                $"AI tower-rebuild obstruction marked for safe deletion: player={playerId}, mapper={mapperValue}, ruinType={ruinType}, buildingId={buildingId}, globalId={globalId}, tileId={tileId}.");
            return $"blocked-tower-ruin-marked:buildingId={buildingId},globalId={globalId},type={ruinType}";
        }

        private void LogDiagnostic(int playerId, int mapperValue, int tileId, int vanillaResult, string outcome)
        {
            int now = SafeCurrentTick();
            string category = outcome;
            int separator = category.IndexOf(':');
            if (separator >= 0)
                category = category.Substring(0, separator);
            var key = new DiagnosticKey(playerId, mapperValue, tileId, vanillaResult != 0, category);
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
                $"AI tower placement validator evaluated: player={playerId}, mapper={mapperValue}, tileId={tileId}, vanillaResult={vanillaResult}, outcome={outcome}, tick={now}.");
        }

        private bool IsEnabled =>
            settings.EnableMod && settings.EnableAiFixes && settings.FixAITowerRepair;

        private static bool IsLiveTowerMapper(int mapperValue)
        {
            eMappers mapper = (eMappers)mapperValue;
            return mapper == eMappers.MAPPER_TOWER ||
                ((int)mapper >= (int)eMappers.MAPPER_TOWER1 && (int)mapper <= (int)eMappers.MAPPER_TOWER5);
        }

        private static bool IsTowerRuin(eStructs type) =>
            type == eStructs.STRUCT_TOWER5_DESTROYED ||
            ((int)type >= (int)eStructs.STRUCT_TOWER1_DESTROYED && (int)type <= (int)eStructs.STRUCT_TOWER4_DESTROYED);

        private static int SafeCurrentTick()
        {
            try { return GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick; }
            catch { return -1; }
        }

        private static int ElapsedTicks(int now, int previous) =>
            unchecked((int)Math.Min((uint)(now - previous), int.MaxValue));

        private readonly struct DiagnosticKey : IEquatable<DiagnosticKey>
        {
            internal DiagnosticKey(int playerId, int mapper, int tileId, bool blocked, string outcome)
            {
                PlayerId = playerId;
                Mapper = mapper;
                TileId = tileId;
                Blocked = blocked;
                Outcome = outcome ?? string.Empty;
            }

            private int PlayerId { get; }
            private int Mapper { get; }
            private int TileId { get; }
            private bool Blocked { get; }
            private string Outcome { get; }

            public bool Equals(DiagnosticKey other) =>
                PlayerId == other.PlayerId && Mapper == other.Mapper && TileId == other.TileId &&
                Blocked == other.Blocked && string.Equals(Outcome, other.Outcome, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is DiagnosticKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = PlayerId;
                    hash = (hash * 397) ^ Mapper;
                    hash = (hash * 397) ^ TileId;
                    hash = (hash * 397) ^ Blocked.GetHashCode();
                    return (hash * 397) ^ Outcome.GetHashCode();
                }
            }
        }
    }
}
