// Feature: Allow churches and cathedrals to employ additional priests.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace ExtraFeatures
{
    internal sealed unsafe class ChurchPriestCountRuntime
    {
        private const int Church1WorkerCount = 1;
        private const int Church2VanillaWorkerCount = 1;
        private const int Church3VanillaWorkerCount = 1;
        private const int Church2ModdedWorkerCount = 2;
        private const int Church3ModdedWorkerCount = 3;
        private const int PatternTableStartIndex = 30;
        private const int WorkerTablePatternRva = 0x2E5E58;

        private static readonly byte[] WorkerTablePattern = BuildWorkerTablePattern();

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private IntPtr workerCountTable;
        private bool initialized;

        public ChurchPriestCountRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void InitializeNative(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            if (initialized)
                return;

            workerCountTable = FindWorkerCountTable(
                libraryHandle,
                memory,
                referenceHashMatches);
            ValidateWorkerCounts();
            initialized = true;
            LogInfo($"church worker table found at {unchecked((ulong)workerCountTable.ToInt64()).ToString("X16", CultureInfo.InvariantCulture)}.");
        }

        public void ApplySetting()
        {
            if (!initialized)
                return;

            bool enabled = Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) && settings.EnableExtraChurchPriests;
            SetDefaultWorkerCount(eStructs.STRUCT_CHURCH2, enabled ? Church2ModdedWorkerCount : Church2VanillaWorkerCount);
            SetDefaultWorkerCount(eStructs.STRUCT_CHURCH3, enabled ? Church3ModdedWorkerCount : Church3VanillaWorkerCount);
            ApplyExistingChurches(enabled);
            LogInfo($"church priest counts applied: enabled={enabled}, church2={GetDefaultWorkerCount(eStructs.STRUCT_CHURCH2)}, church3={GetDefaultWorkerCount(eStructs.STRUCT_CHURCH3)}.");
        }

        public void ApplySpawnedBuilding(BuildingSpawnEventArgs args)
        {
            if (!initialized || args == null || args.Phase != EventHookPhase.Post || args.ReturnValue <= 0)
                return;

            bool enabled = Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) && settings.EnableExtraChurchPriests;
            if (!TryGetRequiredWorkerCount(args.Building, enabled, out ushort requiredWorkers))
                return;

            ApplyBuildingWorkerRequirement((int)args.ReturnValue, args.Building, requiredWorkers);
        }

        private void ApplyExistingChurches(bool enabled)
        {
            ApplyExistingChurchType(eStructs.STRUCT_CHURCH2, enabled ? Church2ModdedWorkerCount : Church2VanillaWorkerCount);
            ApplyExistingChurchType(eStructs.STRUCT_CHURCH3, enabled ? Church3ModdedWorkerCount : Church3VanillaWorkerCount);
        }

        private void ApplyExistingChurchType(eStructs structure, int requiredWorkers)
        {
            List<int> buildingIds = new List<int>();
            GameBuildingManagerAPI.Instance.GetAllBuildings(buildingIds, AliveState.IsAlive, structure);

            for (int i = 0; i < buildingIds.Count; i++)
            {
                if (!TryResolveBuildingId(buildingIds[i], structure, out int buildingId))
                    continue;

                ApplyBuildingWorkerRequirement(buildingId, structure, (ushort)requiredWorkers);
            }
        }

        private void ApplyBuildingWorkerRequirement(int buildingId, eStructs expectedStructure, ushort requiredWorkers)
        {
            try
            {
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building))
                    return;

                if (building->r_AliveState != AliveState.IsAlive || building->r_BuildingType != expectedStructure)
                    return;

                building->r_TotalWorkersRequired = requiredWorkers;
                building->r_TotalMissingWorkers = (ushort)Math.Max(0, requiredWorkers - building->r_TotalCurrentWorkers);
            }
            catch (Exception ex)
            {
                LogError($"could not apply church worker requirement: buildingId={buildingId}, structure={expectedStructure}, requiredWorkers={requiredWorkers}, error={ex}");
            }
        }

        private bool TryResolveBuildingId(int queryId, eStructs expectedStructure, out int buildingId)
        {
            if (IsExpectedBuilding(queryId, expectedStructure))
            {
                buildingId = queryId;
                return true;
            }

            buildingId = 0;
            LogError(
                $"building query returned an invalid 1-based game ID: queryId={queryId}, " +
                $"expectedStructure={expectedStructure}.");
            return false;
        }

        private static bool IsExpectedBuilding(int buildingId, eStructs expectedStructure)
        {
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building))
                return false;

            return building->r_AliveState == AliveState.IsAlive && building->r_BuildingType == expectedStructure;
        }

        private static bool TryGetRequiredWorkerCount(eStructs structure, bool enabled, out ushort requiredWorkers)
        {
            switch (structure)
            {
                case eStructs.STRUCT_CHURCH2:
                    requiredWorkers = (ushort)(enabled ? Church2ModdedWorkerCount : Church2VanillaWorkerCount);
                    return true;
                case eStructs.STRUCT_CHURCH3:
                    requiredWorkers = (ushort)(enabled ? Church3ModdedWorkerCount : Church3VanillaWorkerCount);
                    return true;
                default:
                    requiredWorkers = 0;
                    return false;
            }
        }

        private void SetDefaultWorkerCount(eStructs structure, int workers)
        {
            Marshal.WriteInt32(GetWorkerCountAddress(structure), workers);
        }

        private int GetDefaultWorkerCount(eStructs structure)
        {
            return Marshal.ReadInt32(GetWorkerCountAddress(structure));
        }

        private IntPtr GetWorkerCountAddress(eStructs structure)
        {
            return IntPtr.Add(workerCountTable, (int)structure * sizeof(int));
        }

        private void ValidateWorkerCounts()
        {
            if (GetDefaultWorkerCount(eStructs.STRUCT_CHURCH1) != Church1WorkerCount ||
                GetDefaultWorkerCount(eStructs.STRUCT_CHURCH2) != Church2VanillaWorkerCount ||
                GetDefaultWorkerCount(eStructs.STRUCT_CHURCH3) != Church3VanillaWorkerCount)
            {
                throw new InvalidOperationException(
                    $"The church worker table has unexpected vanilla values: church1={GetDefaultWorkerCount(eStructs.STRUCT_CHURCH1)}, church2={GetDefaultWorkerCount(eStructs.STRUCT_CHURCH2)}, church3={GetDefaultWorkerCount(eStructs.STRUCT_CHURCH3)}.");
            }
        }

        private IntPtr FindWorkerCountTable(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            int matchOffset = Shared.NativePatternResolver.ResolveUnique(
                memory,
                WorkerTablePattern,
                WorkerTablePatternRva,
                referenceHashMatches,
                "default worker-count table",
                log,
                Shared.NativePatternSearchScope.EntireImage).Rva;

            return IntPtr.Add(libraryHandle, matchOffset - PatternTableStartIndex * sizeof(int));
        }

        private static byte[] BuildWorkerTablePattern()
        {
            int[] values =
            {
                1, 1, 1, 1, 3, 0, 1, 1, 1, 0
            };
            byte[] pattern = new byte[values.Length * sizeof(int)];
            for (int i = 0; i < values.Length; i++)
                BitConverter.GetBytes(values[i]).CopyTo(pattern, i * sizeof(int));

            return pattern;
        }

        private void LogInfo(string message)
        {
            log.LogInfo($"[{TimestampNow()}] Extra Features {message}");
        }

        private void LogError(string message)
        {
            log.LogError($"[{TimestampNow()}] Extra Features {message}");
        }

        private static string TimestampNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }
    }
}
