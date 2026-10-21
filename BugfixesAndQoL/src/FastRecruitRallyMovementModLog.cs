// Feature: Timestamped diagnostics for Fast Recruit Rally Movement.
using BepInEx.Logging;
using System;

namespace BugfixesAndQoL
{
    internal static class FastRecruitRallyMovementModLog
    {
        public static void Debug(ManualLogSource log, string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0)
        {
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebugLimited(log, callerFile + ":" + callerLine, message);
        }

        public static void Error(ManualLogSource log, string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0)
        {
            Shared.DebugLogHelper.LogErrorLimited(log, callerFile + ":" + callerLine, message);
        }
    }
}
