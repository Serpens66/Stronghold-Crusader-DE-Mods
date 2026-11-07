// Feature: Timestamped logging helpers for the Troop Speed Fix.
using BepInEx.Logging;

namespace Pathfinding
{
    internal static class TroopMovementFixModLog
    {
        public static void Debug(ManualLogSource log, string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0)
        {
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebugLimited(log, callerFile + ":" + callerLine, message);
        }

        public static void Warning(ManualLogSource log, string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0)
        {
            Shared.DebugLogHelper.LogWarningLimited(log, callerFile + ":" + callerLine, message);
        }
    }
}
