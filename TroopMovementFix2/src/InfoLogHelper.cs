using BepInEx.Logging;
using System;
using System.Globalization;

namespace Shared
{
    // The reused native hook calls the historical LogDebug methods. In Fix2 they
    // intentionally route to Info so every operational diagnostic is visible
    // without changing the old TroopMovementFix source.
    internal static class DebugLogHelper
    {
        public static bool IsDebugEnabled()
        {
            return true;
        }

        public static void LogDebug(ManualLogSource log, params object[] parts)
        {
            if (log == null)
                return;

            log.LogInfo(WithTimestamp(string.Join(" ", parts)));
        }

        public static void LogDebug(ManualLogSource log, Func<string> messageFactory)
        {
            if (log == null || messageFactory == null)
                return;

            log.LogInfo(WithTimestamp(messageFactory()));
        }

        public static void LogInfo(ManualLogSource log, string message)
        {
            log?.LogInfo(WithTimestamp(message));
        }

        public static void LogWarning(ManualLogSource log, string message)
        {
            log?.LogWarning(WithTimestamp(message));
        }

        public static void LogError(ManualLogSource log, string message)
        {
            log?.LogError(WithTimestamp(message));
        }

        private static string WithTimestamp(string message)
        {
            return $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}] {message ?? string.Empty}";
        }
    }
}
