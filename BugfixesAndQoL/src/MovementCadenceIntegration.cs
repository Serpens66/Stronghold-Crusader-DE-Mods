// Feature: Internal coordination between troop cadence and fast-recruit movement.
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace BugfixesAndQoL
{
    internal static class MovementCadenceIntegration
    {
        private static Action<IntPtr> applyFastRecruitMaximumSpeed;
        private static Func<IntPtr, bool> tryApplyFastRecruitCadence;
        private static SynchronizedMovementCadencePatch cadencePatch;

        internal static event Action RegistrationChanged;

        internal static bool HasFastRecruitCallbacks =>
            applyFastRecruitMaximumSpeed != null && tryApplyFastRecruitCadence != null;

        internal static bool IsReady => cadencePatch != null;

        internal static bool RegisterFastRecruitCallbacks(
            Action<IntPtr> applyMaximumSpeed,
            Func<IntPtr, bool> tryApplyCadence)
        {
            applyFastRecruitMaximumSpeed = applyMaximumSpeed ?? throw new ArgumentNullException(nameof(applyMaximumSpeed));
            tryApplyFastRecruitCadence = tryApplyCadence ?? throw new ArgumentNullException(nameof(tryApplyCadence));
            try
            {
                RegistrationChanged?.Invoke();
            }
            catch
            {
                applyFastRecruitMaximumSpeed = null;
                tryApplyFastRecruitCadence = null;
                throw;
            }
            return IsReady;
        }

        internal static void UnregisterFastRecruitCallbacks(Action<IntPtr> applyMaximumSpeed)
        {
            // Only the current owner may remove the process-wide callbacks.
            if (applyFastRecruitMaximumSpeed != applyMaximumSpeed)
                return;

            applyFastRecruitMaximumSpeed = null;
            tryApplyFastRecruitCadence = null;
            RegistrationChanged?.Invoke();
        }

        internal static bool SupportsSynchronizedRunning(int unitType)
        {
            return cadencePatch != null && cadencePatch.SupportsSynchronizedRunning((eChimps)unitType);
        }

        internal static ushort GetNativeRunningSpeedBonus(int unitType, bool improvedSpearmen)
        {
            return cadencePatch?.GetNativeRunningSpeedBonus((eChimps)unitType, improvedSpearmen) ?? 0;
        }

        internal static bool TryGetNativeRunningSpeedBonus(
            int unitType,
            bool improvedSpearmen,
            out ushort runningSpeedBonus)
        {
            runningSpeedBonus = 0;
            return cadencePatch != null &&
                cadencePatch.TryGetNativeRunningSpeedBonus(
                    (eChimps)unitType,
                    improvedSpearmen,
                    out runningSpeedBonus);
        }

        internal static bool TryGetNativeRunningState(int unitType, uint currentState, out uint runningState)
        {
            runningState = currentState;
            return cadencePatch != null &&
                cadencePatch.TryGetNativeRunningState((eChimps)unitType, currentState, out runningState);
        }

        internal static void SetCadencePatch(SynchronizedMovementCadencePatch patch)
        {
            cadencePatch = patch;
        }

        internal static unsafe void ApplyFastRecruitMaximumSpeed(GameUnit* unit)
        {
            applyFastRecruitMaximumSpeed?.Invoke(new IntPtr(unit));
        }

        internal static unsafe bool TryApplyFastRecruitCadence(SHCDESE.Interop.GameUnit* unit)
        {
            return tryApplyFastRecruitCadence?.Invoke(new IntPtr(unit)) == true;
        }
    }
}
