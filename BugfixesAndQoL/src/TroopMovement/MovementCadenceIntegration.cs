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
        private static bool rallyEnabled;

        internal static event Action StateChanged;

        internal static bool HasFastRecruitCallbacks =>
            fastRecruitOwner != null;

        internal static bool IsReady => cadencePatch != null;

        internal static bool IsRallyEnabled =>
            fastRecruitOwner != null && rallyEnabled;

        internal static bool RegisterFastRecruitOwner(object owner)
        {
            fastRecruitOwner = owner ?? throw new ArgumentNullException(nameof(owner));
            try
            {
                StateChanged?.Invoke();
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
            rallyEnabled = false;
            fastRecruitOwner = null;
            StateChanged?.Invoke();
        }

        internal static void SetRallyEnabled(bool enabled)
        {
            bool effectiveEnabled = enabled && fastRecruitOwner != null;
            cadencePatch?.SetRallyEnabled(effectiveEnabled);
            if (rallyEnabled == effectiveEnabled)
                return;

            rallyEnabled = effectiveEnabled;
            StateChanged?.Invoke();
        }

        internal static void SetCadencePatch(SynchronizedMovementCadencePatch patch)
        {
            cadencePatch = patch;
            cadencePatch?.SetRallyEnabled(rallyEnabled);
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
