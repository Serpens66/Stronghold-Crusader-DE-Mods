// Feature: Timestamped logging helpers for the Troop Speed Fix.
using BepInEx.Logging;
using System;
using System.Globalization;

namespace BugfixesAndQoL
{
    internal static class TroopMovementFix3ModLog
    {
        public static void Debug(ManualLogSource log, string message)
        {
            log?.LogDebug(WithTimestamp(message));
        }

        public static void Info(ManualLogSource log, string message)
        {
            log?.LogInfo(WithTimestamp(message));
        }

        public static void Warning(ManualLogSource log, string message)
        {
            log?.LogWarning(WithTimestamp(message));
        }

        public static void Error(ManualLogSource log, string message)
        {
            log?.LogError(WithTimestamp(message));
        }

        private static string WithTimestamp(string message)
        {
            return $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}] {message ?? string.Empty}";
        }
    }
}
