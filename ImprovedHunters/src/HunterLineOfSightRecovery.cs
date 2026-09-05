using BepInEx.Logging;
using SHCDESE.Interop;
using System;
using RedBird.Core.Memory;

namespace ImprovedHunters
{
    /// <summary>
    /// Fail-closed integration point for the unfinished pre-shot recovery.
    /// The previous managed A* implementation could freeze the game thread on
    /// unreachable destinations and must not issue movement until bounded native
    /// reachability semantics have been identified and validated.
    /// </summary>
    internal sealed class HunterLineOfSightRecovery : IDisposable
    {
        public HunterLineOfSightRecovery(ManualLogSource log, ImprovedHuntersViewModel settings)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            Shared.DebugLogHelper.LogWarning(
                log,
                "Improved Hunters line-of-sight recovery is disabled: " +
                "reason=unbounded-managed-pathfinder-can-freeze, nativeMoveOrders=False.");
        }

        public bool IsAvailable => false;

        public void RecordProjectileSpawn(int hunterUnitId, long timestamp)
        {
        }

        public bool TryRecoverAfterTargetAbort(
            SimpleNativeArray<GameUnit> units,
            int hunterUnitId,
            int targetUnitId,
            uint targetGlobalId,
            long timestamp)
        {
            return false;
        }

        public void ResetForMap()
        {
        }

        public void Dispose()
        {
        }
    }
}
