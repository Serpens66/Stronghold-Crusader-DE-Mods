using SHCDESE.API.LowLevel;
using System;

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLRuntime
    {
        private static LordTeleportGuard.LordTeleportGuardRuntime processLordTeleportGuard;
        private static bool lordTeleportGuardAttempted;

        private void InitializeLordTeleportGuard(CrusaderLibraryLoadContext context)
        {
            if (lordTeleportGuardAttempted) return;
            lordTeleportGuardAttempted = true;
            try
            {
                processLordTeleportGuard = new LordTeleportGuard.LordTeleportGuardRuntime(log, context,
                    settings.EnableMod, settings.EnableLordReturnTeleportFix);
            }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogError(log, "LORD_TELEPORT_GUARD_UNAVAILABLE: human Lord teleport protection is unavailable; " + error);
            }
        }

    }
}
