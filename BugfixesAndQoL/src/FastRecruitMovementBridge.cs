// Feature: Connect Fast Recruit Rally Movement directly to this mod's movement hook.
using BepInEx.Logging;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace BugfixesAndQoL
{
    internal interface IMovementCadenceServices
    {
        bool TryGetNativeRunningSpeedBonus(eChimps unitType, bool improvedSpearmen, out ushort runningSpeedBonus);
        bool TryGetNativeRunningState(eChimps unitType, uint currentState, out uint runningState);
    }

    internal sealed class FastRecruitMovementBridge : IMovementCadenceServices, IDisposable
    {
        private readonly ManualLogSource log;
        private readonly Action<IntPtr> applyMaximumSpeedCallback;
        private readonly Func<IntPtr, bool> tryApplyCadenceCallback;
        private readonly FastRecruitRallyMovementRuntime runtime;
        private bool registered;

        public FastRecruitMovementBridge(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            try
            {
                runtime = new FastRecruitRallyMovementRuntime(log, this);
                applyMaximumSpeedCallback = runtime.ApplyMaximumSpeed;
                tryApplyCadenceCallback = runtime.TryApplyRunningCadence;
                registered = MovementCadenceIntegration.RegisterFastRecruitCallbacks(
                    applyMaximumSpeedCallback,
                    tryApplyCadenceCallback);
                if (!registered)
                {
                    LogError("Fast Recruit Rally Movement was disabled because the movement hook could not be initialized.");
                    MovementCadenceIntegration.UnregisterFastRecruitCallbacks(applyMaximumSpeedCallback);
                    runtime.Dispose();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (applyMaximumSpeedCallback != null)
                        MovementCadenceIntegration.UnregisterFastRecruitCallbacks(applyMaximumSpeedCallback);
                }
                catch
                {
                    // Preserve the original integration failure in the log.
                }
                LogError($"Fast Recruit Rally Movement integration failed and only this feature was disabled: {ex}");
                runtime?.Dispose();
            }
        }

        public bool IsActive => registered;

        public bool TryGetNativeRunningSpeedBonus(eChimps unitType, bool improvedSpearmen, out ushort runningSpeedBonus)
        {
            runningSpeedBonus = 0;
            return registered && MovementCadenceIntegration.TryGetNativeRunningSpeedBonus(
                (int)unitType,
                improvedSpearmen,
                out runningSpeedBonus);
        }

        public bool TryGetNativeRunningState(eChimps unitType, uint currentState, out uint runningState)
        {
            runningState = currentState;
            return registered && MovementCadenceIntegration.TryGetNativeRunningState(
                (int)unitType,
                currentState,
                out runningState);
        }

        public void Dispose()
        {
            if (registered)
                MovementCadenceIntegration.UnregisterFastRecruitCallbacks(applyMaximumSpeedCallback);
            registered = false;
            runtime?.Dispose();
        }

        private void LogError(string message)
        {
            log.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Bugfixes and QoL {message}");
        }
    }
}
