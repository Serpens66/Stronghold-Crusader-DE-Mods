// Feature: Let Vanilla AI tower rebuilding clear its own blocking tower ruin.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
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

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int BuildingPlacementValidatorDelegate(
            ulong placementStateAddress,
            int tileId,
            int playerId,
            int mapperValue,
            int mode);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly HashSet<int> markedBuildingIds = new HashSet<int>();
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<BuildingPlacementValidatorDelegate>> validatorHook =
            new HookRef<X64ManagedFunctionDetourAOB<BuildingPlacementValidatorDelegate>>();
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
            markedBuildingIds.Clear();
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
                // A result of one is Vanilla's blocked-tile result. Player zero is used by
                // candidate-fit probes; requiring a real AI slot limits deletion to live builds.
                if (result != 0 && IsEnabled && IsLiveTowerMapper(mapperValue) &&
                    playerId >= 1 && playerId <= 8 &&
                    GamePlayerManagerAPI.Instance.IsAIPlayer(playerId))
                {
                    TryMarkBlockingRuin(tileId, playerId, mapperValue);
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

        private void TryMarkBlockingRuin(int tileId, int playerId, int mapperValue)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsValidTileId(tileId))
                return;
            int buildingId = tileApi.GetTileBuildingId(tileId);
            if (buildingId <= 0 || markedBuildingIds.Contains(buildingId))
                return;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building->r_PlayerIdOwner != playerId ||
                !IsTowerRuin(building->r_BuildingType))
            {
                return;
            }

            if (!buildingApi.DeleteBuildingSafe(buildingId))
                return;

            markedBuildingIds.Add(buildingId);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AI tower-rebuild obstruction marked for safe deletion: player={playerId}, mapper={mapperValue}, ruinType={building->r_BuildingType}, buildingId={buildingId}, globalId={building->r_GlobalId}, tileId={tileId}.");
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
    }
}
