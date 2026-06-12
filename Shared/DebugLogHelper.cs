using BepInEx.Logging;
using System;
using System.Reflection;

namespace Shared
{
    internal static class DebugLogHelper
    {
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

            log.LogDebug(string.Join(" ", parts));
        }

        public static void LogDebug(ManualLogSource log, Func<string> messageFactory)
        {
            if (log == null || messageFactory == null || !IsDebugEnabled())
                return;

            log.LogDebug(messageFactory());
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
