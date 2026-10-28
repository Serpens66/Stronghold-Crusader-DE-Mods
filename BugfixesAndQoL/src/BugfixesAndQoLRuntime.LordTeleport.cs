using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLRuntime
    {
        private static LordTeleportGuard.LordTeleportGuardRuntime processLordTeleportGuard;
        private static bool lordTeleportGuardAttempted, lordTeleportObserverFailureLogged;

        private void InitializeLordTeleportGuard(CrusaderLibraryLoadContext context)
        {
            if (lordTeleportGuardAttempted) return;
            lordTeleportGuardAttempted = true;
            try
            {
                processLordTeleportGuard = new LordTeleportGuard.LordTeleportGuardRuntime(log, context,
                    settings.EnableMod, settings.EnableLordReturnTeleportFix);
                Shared.DevDiag.Attach(log);
                // Process publisher retains a static handler; Unity startup cleanup never removes this hook.
                GameTimeManagerAPI.Instance.OnTick += OnLordTeleportTick;
                processLordTeleportGuard.ReportInstalled();
            }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogError(log, "LORD_TELEPORT_GUARD_UNAVAILABLE: human Lord teleport protection is unavailable; " + error);
            }
        }

        private static void OnLordTeleportTick(int tick)
        {
            try { processLordTeleportGuard?.OnTick(tick); }
            catch (Exception error)
            {
                if (lordTeleportObserverFailureLogged) return;
                lordTeleportObserverFailureLogged = true;
                Shared.DebugLogHelper.LogError(processLordTeleportGuard.LogSource,
                    "LORD_TELEPORT_GUARD_OBSERVER_FAILED: " + error);
            }
        }
    }
}
