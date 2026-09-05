// Feature: Runtime ownership for features moved from Extra Features.
using System;

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLRuntime
    {
        private QuarryPileRelocationRuntime quarryPileRelocationRuntime;
        private ReachableEnemyGatehouseRuntime reachableEnemyGatehouseRuntime;
        private SingleBuildingPauseHook singleBuildingPauseHook;
        private AIEconomyProtectionHook aiEconomyProtectionHook;
        private FastRecruitMovementBridge fastRecruitMovementBridge;
        private bool fastRecruitInitializationAttempted;
        private bool quarryFixedLayoutErrorLogged;

        public object QuarryPileRelocationButton => quarryPileRelocationRuntime.ButtonViewModel;

        private void InitializeMovedFeatures()
        {
            quarryPileRelocationRuntime = new QuarryPileRelocationRuntime(
                log,
                settings,
                multiplayerFeatureGate);
            reachableEnemyGatehouseRuntime = new ReachableEnemyGatehouseRuntime(log, settings);
        }

        private void InitializeMovedFeatureNetwork()
        {
            InstallSingleBuildingPauseHook();

            // This order is a multiplayer protocol boundary and must not depend on settings.
            quarryPileRelocationRuntime.InitializeNetwork();
            singleBuildingPauseHook.InitializeNetwork();
            reachableEnemyGatehouseRuntime.Initialize();
        }

        private void InitializeMovedFeatureNative(
            IntPtr nativeLibraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            reachableEnemyGatehouseRuntime.SetNativeCompatibility(referenceHashMatches);
            if (referenceHashMatches)
            {
                try
                {
                    quarryPileRelocationRuntime.InstallNativeFunctions(
                        nativeLibraryHandle,
                        memory,
                        referenceHashMatches: true);
                }
                catch (Exception ex)
                {
                    LogMovedFeatureFailure("quarry-pile relocation native functions", ex);
                }
            }

            InstallAIEconomyProtectionHook(nativeLibraryHandle, memory);
        }

        private void ApplyMovedFeatureSettings()
        {
            if (!nativeLibraryAvailable || !settings.EnableMod)
            {
                quarryPileRelocationRuntime.Dispose();
                fastRecruitMovementBridge?.Dispose();
                fastRecruitMovementBridge = null;
                fastRecruitInitializationAttempted = false;
                singleBuildingPauseHook?.ClearOverrides("mod disabled");
                singleBuildingPauseHook?.UninstallLocalHooks();
                return;
            }

            if (!fixedLayoutHashValidated)
            {
                if ((settings.EnableQuarryPileRelocation || settings.EnableAIQuarryPileTowardsKeep) &&
                    !quarryFixedLayoutErrorLogged)
                {
                    quarryFixedLayoutErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        "Bugfixes and QoL quarry-pile relocation remains inactive because its fixed " +
                        "native layout is not validated for this CrusaderDE.dll.");
                }
            }
            else if (settings.EnableQuarryPileRelocation || settings.EnableAIQuarryPileTowardsKeep)
            {
                quarryPileRelocationRuntime.Initialize();
                quarryPileRelocationRuntime.ApplySetting();
            }
            else
            {
                quarryPileRelocationRuntime.Dispose();
            }

            if (settings.EnableSingleBuildingPause)
                singleBuildingPauseHook?.InstallLocalHooks();
            else
            {
                singleBuildingPauseHook?.ClearOverrides("setting disabled");
                singleBuildingPauseHook?.UninstallLocalHooks();
            }

            ApplyFastRecruitRallyMovementSetting();
        }

        private void InstallSingleBuildingPauseHook()
        {
            if (singleBuildingPauseHook != null)
                return;

            singleBuildingPauseHook = new SingleBuildingPauseHook(
                log,
                settings,
                multiplayerFeatureGate);
            if (aiEconomyProtectionHook != null)
            {
                singleBuildingPauseHook.SetSleepStateSynchronizer(
                    aiEconomyProtectionHook.SynchronizeSleepStatesNow);
            }
        }

        private void InstallAIEconomyProtectionHook(
            IntPtr nativeLibraryHandle,
            ReadOnlySpan<byte> memory)
        {
            if (aiEconomyProtectionHook != null)
                return;

            try
            {
                aiEconomyProtectionHook = new AIEconomyProtectionHook(
                    log,
                    settings,
                    nativeRegion,
                    nativeLibraryHandle,
                    memory,
                    fixedLayoutHashValidated);
                singleBuildingPauseHook?.SetSleepStateSynchronizer(
                    aiEconomyProtectionHook.SynchronizeSleepStatesNow);
            }
            catch (Exception ex)
            {
                LogMovedFeatureFailure("AI economy protection hook", ex);
            }
        }

        private void ApplyFastRecruitRallyMovementSetting()
        {
            if (!nativeLibraryAvailable)
                return;

            if (!settings.EnableMod || !settings.EnableFastRecruitRallyMovement)
            {
                fastRecruitMovementBridge?.Dispose();
                fastRecruitMovementBridge = null;
                fastRecruitInitializationAttempted = false;
                return;
            }

            if (fastRecruitMovementBridge != null || fastRecruitInitializationAttempted)
                return;

            fastRecruitInitializationAttempted = true;
            var bridge = new FastRecruitMovementBridge(log);
            if (bridge.IsActive)
                fastRecruitMovementBridge = bridge;
            else
                bridge.Dispose();
        }

        private void ResetMovedFeatureMapState()
        {
            singleBuildingPauseHook?.ClearOverrides("map unload");
        }

        private void DisposeMovedFeatures()
        {
            reachableEnemyGatehouseRuntime?.Dispose();
            quarryPileRelocationRuntime?.Dispose();
            singleBuildingPauseHook?.ClearOverrides("runtime disposed");
            singleBuildingPauseHook?.UninstallLocalHooks();
            aiEconomyProtectionHook?.Dispose();
            aiEconomyProtectionHook = null;
            fastRecruitMovementBridge?.Dispose();
            fastRecruitMovementBridge = null;
        }

        private void LogMovedFeatureFailure(string featureName, Exception ex)
        {
            Shared.DebugLogHelper.LogError(
                log,
                $"Bugfixes and QoL feature '{featureName}' failed and remains inactive: {ex}");
        }
    }
}
