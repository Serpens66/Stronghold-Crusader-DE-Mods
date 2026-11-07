// Feature: Runtime ownership for features moved from Extra Features.
using System;

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLRuntime
    {
        private QuarryPileRelocationRuntime quarryPileRelocationRuntime;
        private SingleBuildingPauseHook singleBuildingPauseHook;
        private AIEconomyProtectionHook aiEconomyProtectionHook;
        private bool quarryFixedLayoutErrorLogged;

        public object QuarryPileRelocationButton => quarryPileRelocationRuntime.ButtonViewModel;

        private void InitializeBuildingAndEconomyFeatures()
        {
            quarryPileRelocationRuntime = new QuarryPileRelocationRuntime(
                log,
                settings,
                multiplayerFeatureGate);
        }

        private void InitializeBuildingFeatureNetwork()
        {
            InstallSingleBuildingPauseHook();

            // This order is a multiplayer protocol boundary and must not depend on settings.
            quarryPileRelocationRuntime.InitializeNetwork();
            singleBuildingPauseHook.InitializeNetwork();
        }

        private void InitializeBuildingAndEconomyNative(
            IntPtr nativeLibraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
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
                    LogBuildingFeatureFailure("quarry-pile relocation native functions", ex);
                }
            }

            InstallAIEconomyProtectionHook(nativeLibraryHandle, memory);
        }

        private void ApplyBuildingAndEconomySettings()
        {
            if (!nativeLibraryAvailable || !settings.EnableMod)
            {
                quarryPileRelocationRuntime.Dispose();
                singleBuildingPauseHook?.ClearOverrides("mod disabled");
                singleBuildingPauseHook?.DeactivateLocalHooks();
                return;
            }

            if (!fixedLayoutHashValidated)
            {
                if ((settings.EnableQuarryPileRelocation || settings.EnableAIQuarryPileTowardsKeep) &&
                    !quarryFixedLayoutErrorLogged)
                {
                    quarryFixedLayoutErrorLogged = true;
                    Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/BugfixesAndQoLRuntime.BuildingAndEconomy/ApplyBuildingAndEconomySettings", "Bugfixes and QoL quarry-pile relocation remains inactive because its fixed " +
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
                singleBuildingPauseHook?.DeactivateLocalHooks();
            }

        }

        private void InstallSingleBuildingPauseHook()
        {
            if (singleBuildingPauseHook != null)
                return;

            singleBuildingPauseHook = new SingleBuildingPauseHook(
                log,
                settings,
                multiplayerFeatureGate);
            WireSingleBuildingPauseNativeBridge();
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
                WireSingleBuildingPauseNativeBridge();
            }
            catch (Exception ex)
            {
                LogBuildingFeatureFailure("AI economy protection hook", ex);
            }
        }

        private void WireSingleBuildingPauseNativeBridge()
        {
            if (singleBuildingPauseHook == null || aiEconomyProtectionHook == null)
                return;

            aiEconomyProtectionHook.SetSingleBuildingPauseHook(singleBuildingPauseHook);
            singleBuildingPauseHook.SetSleepStateBridge(
                aiEconomyProtectionHook.SynchronizeSleepStatesNow,
                aiEconomyProtectionHook.SetSingleBuildingOverrideInterceptionEnabled);
        }

        private void ClearSingleBuildingPauseOverrides()
        {
            singleBuildingPauseHook?.ClearOverrides("map unload");
        }

        private void InitializeSingleBuildingPauseSaveData()
        {
            if (singleBuildingPauseHook == null)
                throw new InvalidOperationException("The single-building pause feature is unavailable.");
            singleBuildingPauseHook.InitializeSaveData();
        }

        private void RestoreSingleBuildingPauseSaveState(Shared.GameplaySessionStartedContext context)
        {
            singleBuildingPauseHook?.RestoreFromSave(context);
        }

        private void LogBuildingFeatureFailure(string featureName, Exception ex)
        {
            Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/BugfixesAndQoLRuntime.BuildingAndEconomy/LogBuildingFeatureFailure", $"Bugfixes and QoL feature '{featureName}' failed and remains inactive: {ex}");
        }
    }
}
