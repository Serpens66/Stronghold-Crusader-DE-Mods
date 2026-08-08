using BepInEx.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace Shared
{
    internal static class DebugLogHelper
    {
        public const string CurrentNativeSha256 =
            "1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B";

        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(1);
        private static DateTime debugEnabledCacheExpiresAtUtc;
        private static bool debugEnabledCache;

        public static bool IsDebugEnabled()
        {
            DateTime now = DateTime.UtcNow;
            if (now < debugEnabledCacheExpiresAtUtc)
                return debugEnabledCache;

            debugEnabledCache = ComputeDebugEnabled();
            debugEnabledCacheExpiresAtUtc = now + CacheDuration;
            return debugEnabledCache;
        }

        public static void LogDebug(ManualLogSource log, params object[] parts)
        {
            if (log == null || !IsDebugEnabled())
                return;

            log.LogDebug(WithTimestamp(string.Join(" ", parts)));
        }

        public static void LogDebug(ManualLogSource log, Func<string> messageFactory)
        {
            if (log == null || messageFactory == null || !IsDebugEnabled())
                return;

            log.LogDebug(WithTimestamp(messageFactory()));
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

        public static bool ReportNativeLibraryVersion(
            ManualLogSource log,
            string componentName,
            bool requireCurrentVersion = false)
        {
            string label = string.IsNullOrWhiteSpace(componentName)
                ? "Mod"
                : componentName;

            try
            {
                string path = Path.Combine(
                    BepInEx.Paths.GameRootPath,
                    "Stronghold Crusader Definitive Edition_Data",
                    "Plugins",
                    "x86_64",
                    "CrusaderDE.dll");
                if (!File.Exists(path))
                {
                    LogError(log, $"{label} cannot verify CrusaderDE.dll because the installed file was not found: path={path}.");
                    return false;
                }

                string actualHash;
                using (FileStream stream = File.OpenRead(path))
                using (SHA256 sha256 = SHA256.Create())
                {
                    actualHash = BitConverter.ToString(sha256.ComputeHash(stream))
                        .Replace("-", string.Empty);
                }

                long fileSize = new FileInfo(path).Length;
                if (string.Equals(actualHash, CurrentNativeSha256, StringComparison.OrdinalIgnoreCase))
                {
                    LogInfo(
                        log,
                        $"{label} verified the installed CrusaderDE.dll: sha256={actualHash}, size={fileSize}, path={path}.");
                    return true;
                }

                string message =
                    $"{label} detected a changed CrusaderDE.dll: expectedSha256={CurrentNativeSha256}, " +
                    $"actualSha256={actualHash}, size={fileSize}, path={path}.";
                if (requireCurrentVersion)
                {
                    LogError(log, message + " Version-sensitive native code remains inactive.");
                }
                else
                {
                    LogWarning(
                        log,
                        message +
                        " Signature-validated code may continue; any failed validation is logged and the affected feature remains inactive.");
                }

                return false;
            }
            catch (Exception ex)
            {
                if (requireCurrentVersion)
                {
                    LogError(log, $"{label} could not verify the installed CrusaderDE.dll; version-sensitive native code must remain inactive: {ex}");
                }
                else
                {
                    LogWarning(
                        log,
                        $"{label} could not verify the installed CrusaderDE.dll hash. " +
                        $"Signature-validated code may continue; any failed validation is logged and the affected feature remains inactive. Reason: {ex}");
                }
                return false;
            }
        }

        public static bool IsCurrentNativeLibraryVersion()
        {
            try
            {
                string path = Path.Combine(
                    BepInEx.Paths.GameRootPath,
                    "Stronghold Crusader Definitive Edition_Data",
                    "Plugins",
                    "x86_64",
                    "CrusaderDE.dll");
                if (!File.Exists(path))
                    return false;

                using (FileStream stream = File.OpenRead(path))
                using (SHA256 sha256 = SHA256.Create())
                {
                    string actualHash = BitConverter.ToString(sha256.ComputeHash(stream))
                        .Replace("-", string.Empty);
                    return string.Equals(
                        actualHash,
                        CurrentNativeSha256,
                        StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // Callers use false to keep only fixed-layout native code inactive.
                return false;
            }
        }

        private static string WithTimestamp(string message)
        {
            return $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}] {message ?? string.Empty}";
        }

        private static bool ComputeDebugEnabled()
        {
            try
            {
                foreach (ILogListener listener in Logger.Listeners)
                {
                    if (IsDebugEnabled(listener))
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool IsDebugEnabled(ILogListener listener)
        {
            if (listener == null)
                return false;

            if (listener is DiskLogListener diskLogListener)
                return HasDebugFlag(diskLogListener.DisplayedLogLevel);

            object displayedLogLevel = TryGetPropertyValue(listener, "DisplayedLogLevel");
            if (displayedLogLevel is LogLevel listenerLogLevel)
                return HasDebugFlag(listenerLogLevel);

            object value = TryGetConfigEntryValue(listener.GetType(), "ConfigConsoleDisplayedLevel");
            return value is LogLevel logLevel && HasDebugFlag(logLevel);
        }

        private static object TryGetPropertyValue(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            return property?.GetValue(instance);
        }

        private static object TryGetConfigEntryValue(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            object configEntry = field?.GetValue(null);
            if (configEntry == null)
                return null;

            PropertyInfo valueProperty = configEntry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            return valueProperty?.GetValue(configEntry);
        }

        private static bool HasDebugFlag(LogLevel logLevel)
        {
            return (logLevel & LogLevel.Debug) != LogLevel.None;
        }
    }
}
