using BepInEx.Logging;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace Shared
{
    internal static class DebugLogHelper
    {
        public const string CurrentNativeSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(1);
        private static DateTime debugEnabledCacheExpiresAtUtc;
        private static bool debugEnabledCache;
        private static readonly LogRateGate debugRate = new LogRateGate();
        private static readonly LogRateGate failureRate = new LogRateGate();

        public static void LogDebugLimited(ManualLogSource log, string site, params object[] parts)
        {
            if (log == null || !IsDebugEnabled() || !debugRate.TryReserve(site, 5, out long suppressed))
                return;
            log.LogDebug(WithTimestamp(WithSuppressed(string.Join(" ", parts), suppressed)));
        }

        public static void LogDebugLimited(ManualLogSource log, string site, Func<string> messageFactory)
        {
            if (log == null || messageFactory == null || !IsDebugEnabled() ||
                !debugRate.TryReserve(site, 5, out long suppressed))
                return;
            log.LogDebug(WithTimestamp(WithSuppressed(messageFactory(), suppressed)));
        }

        public static void LogWarningLimited(ManualLogSource log, string site, params object[] parts) =>
            LogFailureLimited(log, site, false, parts);

        public static void LogErrorLimited(ManualLogSource log, string site, params object[] parts) =>
            LogFailureLimited(log, site, true, parts);

        private static void LogFailureLimited(ManualLogSource log, string site, bool error, object[] parts)
        {
            if (log == null) return;
            Exception exception = null;
            foreach (object part in parts)
                if (part is Exception candidate) exception = candidate;
            string message = null;
            // The call site separates features; exception identity separates causes without
            // retaining whole stack traces or an unbounded set of object IDs.
            string cause;
            if (exception == null)
                cause = message = string.Join(" ", parts);
            else
                cause = string.Join(" ", Array.FindAll(parts, part => part is string || part is Enum)) +
                    ":" + exception.GetType().FullName + ":" + exception.Message;
            if (cause.Length > 2048)
            {
                using (SHA256 hash = SHA256.Create())
                    cause = cause.Substring(0, 2048) + ":" +
                        BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(cause)));
            }
            string key = site + (error ? ":error:" : ":warning:") + cause;
            if (!failureRate.TryReserve(key, 30, out long suppressed)) return;
            message = WithTimestamp(WithSuppressed(message ?? string.Join(" ", parts), suppressed));
            if (error) log.LogError(message); else log.LogWarning(message);
        }

        private static string WithSuppressed(string message, long suppressed) =>
            suppressed == 0 ? message : message + " [suppressed repeats=" + suppressed + "]";

        public static bool IsDebugEnabled()
        {
            DateTime now = DateTime.UtcNow;
            if (now < debugEnabledCacheExpiresAtUtc)
                return debugEnabledCache;

            debugEnabledCache = ComputeDebugEnabled();
            debugEnabledCacheExpiresAtUtc = now + CacheDuration;
            return debugEnabledCache;
        }

        public static bool IsDiskDebugEnabled()
        {
            try
            {
                foreach (ILogListener listener in Logger.Listeners)
                {
                    if (listener is DiskLogListener diskLogListener &&
                        HasDebugFlag(diskLogListener.DisplayedLogLevel))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
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
            bool requireCurrentVersion = false,
            bool logSuccess = true)
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
                    if (logSuccess)
                    {
                        LogDebug(
                            log,
                            $"{label} verified the installed CrusaderDE.dll: sha256={actualHash}, size={fileSize}, path={path}.");
                    }
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

namespace Shared
{
    // Logging state only: monotonic wall time must never affect simulation decisions.
    internal sealed class LogRateGate
    {
        private sealed class Entry
        {
            internal long LastEmission;
            internal long Suppressed;
        }

        private readonly object sync = new object();
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Func<long> clock;
        private readonly long frequency;
        private readonly int capacity;

        internal LogRateGate(int capacity = 4096)
            : this(Stopwatch.GetTimestamp, Stopwatch.Frequency, capacity) { }

        internal LogRateGate(Func<long> clock, long frequency, int capacity)
        {
            if (clock == null || frequency <= 0 || capacity <= 0)
                throw new ArgumentException("A clock, positive frequency and capacity are required.");
            this.clock = clock;
            this.frequency = frequency;
            this.capacity = capacity;
        }

        internal int Count { get { lock (sync) return entries.Count; } }

        internal bool TryReserve(string site, int seconds, out long suppressed)
        {
            lock (sync)
            {
                long now = clock();
                suppressed = 0;
                if (!entries.TryGetValue(site, out Entry entry))
                {
                    // Sites are fixed source identities. Bound changing error signatures too.
                    if (entries.Count == capacity)
                    {
                        string oldest = null;
                        long oldestTime = long.MaxValue;
                        foreach (KeyValuePair<string, Entry> candidate in entries)
                            if (candidate.Value.LastEmission < oldestTime)
                            {
                                oldestTime = candidate.Value.LastEmission;
                                oldest = candidate.Key;
                            }
                        entries.Remove(oldest);
                    }
                    entries.Add(site, new Entry { LastEmission = now });
                    return true;
                }
                if (now - entry.LastEmission < seconds * frequency)
                {
                    if (entry.Suppressed < long.MaxValue) entry.Suppressed++;
                    return false;
                }
                suppressed = entry.Suppressed;
                entry.Suppressed = 0;
                entry.LastEmission = now;
                return true;
            }
        }
    }
}
