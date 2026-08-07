// Feature: Timestamped diagnostics for Fast Recruit Rally Movement.
using BepInEx.Logging;
using System;

namespace ExtraFeatures
{
    internal static class FastRecruitRallyMovementModLog
    {
        public static void Debug(ManualLogSource log, string message)
        {
            Shared.DebugLogHelper.LogDebug(log, message);
        }

        public static void Error(ManualLogSource log, string message)
        {
            log.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Extra Features {message}");
        }
    }
}
