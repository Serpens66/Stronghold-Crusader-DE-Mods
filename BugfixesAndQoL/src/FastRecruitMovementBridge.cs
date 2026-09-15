// Feature: Connect Fast Recruit Rally Movement directly to this mod's movement hook.
using BepInEx.Logging;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace BugfixesAndQoL
{
    internal interface IMovementCadenceServices
    {
        void SetRallyTracking(int unitId, uint globalId, int ownerPlayerId, eChimps expectedUnitType);
        void ClearRallyTracking(int unitId);
        void ClearAllRallyTracking();
    }

    internal sealed class FastRecruitMovementBridge : IMovementCadenceServices, IDisposable
    {
        private readonly ManualLogSource log;
        private readonly FastRecruitRallyMovementRuntime runtime;
        private bool registered;

        public FastRecruitMovementBridge(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            try
            {
                runtime = new FastRecruitRallyMovementRuntime(log, this);
                registered = MovementCadenceIntegration.RegisterFastRecruitOwner(this);
                if (registered)
                {
                    MovementCadenceIntegration.SetRallyEnabled(true);
                }
                else
                {
                    LogError("Fast Recruit Rally Movement was disabled because the movement hook could not be initialized.");
                    MovementCadenceIntegration.UnregisterFastRecruitOwner(this);
                    runtime.Dispose();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    MovementCadenceIntegration.UnregisterFastRecruitOwner(this);
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

        public void SetEnabled(bool enabled)
        {
            if (!registered)
                return;

            if (enabled)
            {
                runtime.SetEnabled(true);
                MovementCadenceIntegration.SetRallyEnabled(true);
            }
            else
            {
                MovementCadenceIntegration.SetRallyEnabled(false);
                runtime.SetEnabled(false);
            }
        }

        public void SetRallyTracking(int unitId, uint globalId, int ownerPlayerId, eChimps expectedUnitType)
        {
            if (registered)
            {
                MovementCadenceIntegration.SetRallyTracking(
                    unitId,
                    globalId,
                    ownerPlayerId,
                    expectedUnitType);
            }
        }

        public void ClearRallyTracking(int unitId)
        {
            if (registered)
                MovementCadenceIntegration.ClearRallyTracking(unitId);
        }

        public void ClearAllRallyTracking()
        {
            if (registered)
                MovementCadenceIntegration.ClearAllRallyTracking();
        }

        public void Dispose()
        {
            if (registered)
            {
                SetEnabled(false);
                return;
            }

            MovementCadenceIntegration.UnregisterFastRecruitOwner(this);
            runtime?.Dispose();
        }

        private void LogError(string message)
        {
            log.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Bugfixes and QoL {message}");
        }
    }
}
