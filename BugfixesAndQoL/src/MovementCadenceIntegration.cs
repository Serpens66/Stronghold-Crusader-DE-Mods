// Feature: Internal coordination between troop cadence and fast-recruit movement.
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace BugfixesAndQoL
{
    internal static class MovementCadenceIntegration
    {
        private static object fastRecruitOwner;
        private static SynchronizedMovementCadencePatch cadencePatch;

        internal static event Action RegistrationChanged;

        internal static bool HasFastRecruitCallbacks =>
            fastRecruitOwner != null;

        internal static bool IsReady => cadencePatch != null;

        internal static bool RegisterFastRecruitOwner(object owner)
        {
            fastRecruitOwner = owner ?? throw new ArgumentNullException(nameof(owner));
            try
            {
                RegistrationChanged?.Invoke();
            }
            catch
            {
                fastRecruitOwner = null;
                throw;
            }
            return IsReady;
        }

        internal static void UnregisterFastRecruitOwner(object owner)
        {
            if (!ReferenceEquals(fastRecruitOwner, owner))
                return;

            cadencePatch?.SetRallyEnabled(false);
            fastRecruitOwner = null;
            RegistrationChanged?.Invoke();
        }

        internal static void SetRallyEnabled(bool enabled)
        {
            cadencePatch?.SetRallyEnabled(enabled);
        }

        internal static void SetCadencePatch(SynchronizedMovementCadencePatch patch)
        {
            cadencePatch = patch;
        }

        internal static void SetRallyTracking(
            int unitId,
            uint globalId,
            int ownerPlayerId,
            eChimps expectedUnitType)
        {
            cadencePatch?.SetRallyTracking(
                unitId,
                globalId,
                ownerPlayerId,
                expectedUnitType);
        }

        internal static void ClearRallyTracking(int unitId)
        {
            cadencePatch?.ClearRallyTracking(unitId);
        }

        internal static void ClearAllRallyTracking()
        {
            cadencePatch?.ClearAllRallyTracking();
        }
    }
}
