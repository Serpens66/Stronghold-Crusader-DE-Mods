using BepInEx.Logging;
using BepInEx.Configuration;
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

        // Uppercase SHA-256 of a whole file, cached per path for this assembly. Unity's Mono hashes
        // CrusaderDE.dll in tens of milliseconds and several startup checks hash the same file. A
        // changed size or write time recomputes it, so a replaced file never reports an old hash.
        private static readonly object fileHashSync = new object();
        private static readonly Dictionary<string, CachedFileHash> fileHashes =
            new Dictionary<string, CachedFileHash>(StringComparer.OrdinalIgnoreCase);

        public static string GetFileSha256(string path)
        {
            string fullPath = Path.GetFullPath(path);
            var info = new FileInfo(fullPath);
            long length = info.Length;
            DateTime written = info.LastWriteTimeUtc;
            lock (fileHashSync)
            {
                if (fileHashes.TryGetValue(fullPath, out CachedFileHash cached) &&
                    cached.Length == length && cached.Written == written)
                    return cached.Hash;
            }

            string hash;
            using (FileStream stream = File.OpenRead(fullPath))
            using (SHA256 sha256 = SHA256.Create())
                hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
            lock (fileHashSync)
                fileHashes[fullPath] = new CachedFileHash(length, written, hash);
            return hash;
        }

        private readonly struct CachedFileHash
        {
            public CachedFileHash(long length, DateTime written, string hash)
            {
                Length = length;
                Written = written;
                Hash = hash;
            }

            public long Length { get; }
            public DateTime Written { get; }
            public string Hash { get; }
        }

        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(1);
        private static DateTime debugEnabledCacheExpiresAtUtc;
        private static bool debugEnabledCache;
        private static readonly LogRateGate debugRate = new LogRateGate();
        private static bool diagnosticsInitialized;
        private static volatile bool diagnosticsEnabled;
        private static readonly Dictionary<ManualLogSource, ManualLogSource> externalLogs =
            new Dictionary<ManualLogSource, ManualLogSource>();

        // Source-linked into each mod: no host dependency and no shared mutable config.
        // A startup snapshot deliberately ignores config edits until the next game start.
        public static bool DiagnosticsEnabled => diagnosticsEnabled;
        internal static bool CrashBreadcrumbsEnabled { get; private set; }

        public static void InitializeDiagnostics(ConfigFile config, ManualLogSource log)
        {
            if (diagnosticsInitialized) return;
            diagnosticsInitialized = true;
            RuntimeHealth.Initialize(log);
            try
            {
                CrashBreadcrumbsEnabled = config.Bind("Diagnostics", "CrashBreadcrumbs", false,
                    "Record crash breadcrumbs to disk. Disabled by default, including builds/releases. Restart required. " +
                    "Independent of routine diagnostic logs and BepInEx Debug listeners.").Value;
                diagnosticsEnabled = config.Bind("Diagnostics", "Enabled", false,
                    "Enable this mod's diagnostic logs and recordings. Disabled by default; restart the game after changing. " +
                    "Special diagnostic options also require this switch, except the independent CrashBreadcrumbs option. Debug output additionally requires a BepInEx Debug listener.").Value;
            }
            catch (Exception ex)
            {
                CrashBreadcrumbsEnabled = false;
                diagnosticsEnabled = false;
                LogError(log, "Diagnostic configuration could not be read; diagnostics remain disabled: " + ex);
            }
        }

        // External services may write routine logs without offering a diagnostic switch.
        // This unregistered source forwards their events to the owning mod's real logger:
        // Info always passes (as in APIShared), only Debug needs diagnostics.
        public static ManualLogSource ForExternalCode(ManualLogSource log)
        {
            if (log == null) return null;
            lock (externalLogs)
            {
                if (externalLogs.TryGetValue(log, out ManualLogSource proxy)) return proxy;
                proxy = new ManualLogSource(log.SourceName);
                proxy.LogEvent += (_, args) =>
                {
                    if ((args.Level & (LogLevel.Warning | LogLevel.Error | LogLevel.Fatal)) != 0)
                        RuntimeHealth.Forward(log, args.Level, Convert.ToString(args.Data, CultureInfo.InvariantCulture));
                    else if ((args.Level & LogLevel.Debug) == 0 || IsDebugEnabled())
                        try { log.Log(args.Level, WithTimestamp(Convert.ToString(args.Data, CultureInfo.InvariantCulture))); } catch { }
                };
                externalLogs.Add(log, proxy);
                return proxy;
            }
        }

        public static void LogDebugLimited(ManualLogSource log, string site, params object[] parts)
        {
            if (log == null || !IsDebugEnabled() || !debugRate.TryReserve(site, 5, out long suppressed))
                return;
            try { log.LogDebug(WithTimestamp(WithSuppressed(string.Join(" ", parts), suppressed))); } catch { }
        }

        public static void LogDebugLimited(ManualLogSource log, string site, Func<string> messageFactory)
        {
            if (log == null || messageFactory == null || !IsDebugEnabled() ||
                !debugRate.TryReserve(site, 5, out long suppressed))
                return;
            try { log.LogDebug(WithTimestamp(WithSuppressed(messageFactory(), suppressed))); } catch { }
        }

        public static void LogWarningLimited(ManualLogSource log, string site, params object[] parts) =>
            LogFailureLimited(log, site, false, parts);

        public static void LogErrorLimited(ManualLogSource log, string site, params object[] parts) =>
            LogFailureLimited(log, site, true, parts);

        private static void LogFailureLimited(ManualLogSource log, string site, bool error, object[] parts)
        {
            if (log == null) return;
            RuntimeHealth.Failure(log, site, error, string.Join(" ", parts));
        }

        private static string WithSuppressed(string message, long suppressed) =>
            suppressed == 0 ? message : message + " [suppressed repeats=" + suppressed + "]";

        public static bool IsDebugEnabled()
        {
            if (!DiagnosticsEnabled) return false;
            DateTime now = DateTime.UtcNow;
            if (now < debugEnabledCacheExpiresAtUtc)
                return debugEnabledCache;

            debugEnabledCache = ComputeDebugEnabled();
            debugEnabledCacheExpiresAtUtc = now + CacheDuration;
            return debugEnabledCache;
        }

        public static bool IsDiskDebugEnabled()
        {
            if (!DiagnosticsEnabled) return false;
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

            try { log.LogDebug(WithTimestamp(string.Join(" ", parts))); } catch { }
        }

        public static void LogDebug(ManualLogSource log, Func<string> messageFactory)
        {
            if (log == null || messageFactory == null || !IsDebugEnabled())
                return;

            try { log.LogDebug(WithTimestamp(messageFactory())); } catch { }
        }

        // Info is routine release logging (one-time status, user actions) and is never gated;
        // per-map, per-command or detailed output belongs to LogDebug.
        public static void LogInfo(ManualLogSource log, string message)
        {
            try { log?.LogInfo(WithTimestamp(message)); } catch { }
        }

        public static void LogInfo(ManualLogSource log, Func<string> messageFactory)
        {
            if (log == null || messageFactory == null) return;
            try { log.LogInfo(WithTimestamp(messageFactory())); } catch { }
        }

        public static void LogWarning(ManualLogSource log, string message,
            [System.Runtime.CompilerServices.CallerMemberName] string feature = "",
            [System.Runtime.CompilerServices.CallerFilePath] string file = "")
        {
            RuntimeHealth.Failure(log, Path.GetFileNameWithoutExtension(file) + "." + feature, false, message);
        }

        public static void LogError(ManualLogSource log, string message,
            [System.Runtime.CompilerServices.CallerMemberName] string feature = "",
            [System.Runtime.CompilerServices.CallerFilePath] string file = "")
        {
            RuntimeHealth.Failure(log, Path.GetFileNameWithoutExtension(file) + "." + feature, true, message);
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

                string actualHash = GetFileSha256(path);
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

                return string.Equals(
                    GetFileSha256(path),
                    CurrentNativeSha256,
                    StringComparison.OrdinalIgnoreCase);
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
    // Runs only at a failure boundary. Cache eviction never prevents a new report.
    internal sealed class RuntimeHealthLedger
    {
        // Same severity tags as APIShared's ledger: errors are failures, warnings are not.
        internal const string FailureTag = "MOD_FAILURE", WarningTag = "MOD_WARNING";

        private sealed class Entry
        {
            internal LinkedListNode<string> Node;
            internal long EmittedAt, Suppressed;
        }
        private readonly object sync = new object();
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly LinkedList<string> recent = new LinkedList<string>();
        private readonly string owner;
        private readonly Action<LogLevel, string> write;
        private readonly Func<long> clock;
        private readonly long interval;
        private readonly int capacity;
        internal RuntimeHealthLedger(string owner, Action<LogLevel, string> write)
            : this(owner, write, System.Diagnostics.Stopwatch.GetTimestamp, System.Diagnostics.Stopwatch.Frequency, 4096) { }
        internal RuntimeHealthLedger(string owner, Action<LogLevel, string> write, Func<long> clock, long frequency, int capacity = 4096)
        {
            if (clock == null || frequency <= 0 || frequency > long.MaxValue / 30 || capacity <= 0)
                throw new ArgumentException("A monotonic clock, valid frequency and positive capacity are required.");
            this.owner = owner ?? "unknown"; this.write = write; this.clock = clock;
            interval = frequency * 30; this.capacity = capacity;
        }
        private const int CauseCacheCapacity = 64, MaximumCachedCauseLength = 4096;
        private readonly Dictionary<string, string> causeCache = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Queue<string> causeCacheOrder = new Queue<string>();
        internal int CachedCauseCount { get { lock (sync) return causeCache.Count; } }
        internal long CauseHashComputations { get { return System.Threading.Interlocked.Read(ref causeHashComputations); } }
        private long causeHashComputations;

        private string CachedCause(string normalized)
        {
            bool cacheable = normalized.Length <= MaximumCachedCauseLength;
            string cause;
            if (cacheable)
                lock (sync) if (causeCache.TryGetValue(normalized, out cause)) return cause;
            System.Threading.Interlocked.Increment(ref causeHashComputations);
            cause = HashCause(normalized);
            if (cacheable)
                lock (sync)
                {
                    // Another reporting thread may have inserted the same complete cause.
                    if (!causeCache.ContainsKey(normalized))
                    {
                        if (causeCache.Count == CauseCacheCapacity) causeCache.Remove(causeCacheOrder.Dequeue());
                        causeCache.Add(normalized, cause);
                        causeCacheOrder.Enqueue(normalized);
                    }
                }
            return cause;
        }

        internal int Count { get { lock (sync) return entries.Count; } }
        // Repeated failures are the common case; parse the pattern once and reuse one hasher per thread.
        private static readonly System.Text.RegularExpressions.Regex TimestampPattern =
            new System.Text.RegularExpressions.Regex(@"\[\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d{3}\]");
        [ThreadStatic] private static System.Security.Cryptography.SHA256 causeHasher;
        internal void Report(string feature, bool error, string detail)
        {
            try
            {
                feature = Clean(feature);
                // Hash the complete normalized cause: truncating display text must not merge different failures.
                string raw = detail ?? "";
                string normalized = (raw.IndexOf('[') < 0 ? raw : TimestampPattern.Replace(raw, ""))
                    .Replace('\r', ' ').Replace('\n', ' ');
                string cause = CachedCause(normalized);
                string key = feature + (error ? "\nerror\n" : "\nwarning\n") + cause;
                long suppressed = 0;
                lock (sync)
                {
                    long now = clock();
                    if (entries.TryGetValue(key, out var entry))
                    {
                        recent.Remove(entry.Node); recent.AddLast(entry.Node);
                        if (now - entry.EmittedAt < interval)
                        {
                            if (entry.Suppressed < long.MaxValue) entry.Suppressed++;
                            return;
                        }
                        suppressed = entry.Suppressed; entry.Suppressed = 0; entry.EmittedAt = now;
                    }
                    else
                    {
                        if (entries.Count >= capacity) { entries.Remove(recent.First.Value); recent.RemoveFirst(); }
                        entries.Add(key, new Entry { Node = recent.AddLast(key), EmittedAt = now });
                    }
                }
                string message = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                    "] " + (error ? FailureTag : WarningTag) + " mod=" + owner + "; feature=" + feature + "; cause=" + cause + "; detail=" + Clean(detail) +
                    (suppressed == 0 ? "" : "; suppressed repeats=" + suppressed);
                try { write?.Invoke(error ? LogLevel.Error : LogLevel.Warning, message); } catch { }
            }
            catch { /* Logging cannot interrupt gameplay or cleanup. */ }
        }
        private static string HashCause(string normalized)
        {
            System.Security.Cryptography.SHA256 hash = causeHasher ?? (causeHasher = System.Security.Cryptography.SHA256.Create());
            return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(normalized))).Replace("-", "");
        }
        private static string Clean(string value)
        {
            value = (value ?? "").Replace('\r', ' ').Replace('\n', ' ');
            return value.Length <= 4096 ? value : value.Substring(0, 4096);
        }
    }

    internal static class RuntimeHealth
    {
        private static RuntimeHealthLedger ledger;
        private static ManualLogSource logger;
        private static System.Threading.SynchronizationContext context;
        private static int mainThread;
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ManualLogSource, RuntimeHealthLedger> fallbackLedgers =
            new System.Runtime.CompilerServices.ConditionalWeakTable<ManualLogSource, RuntimeHealthLedger>();
        internal static bool IsInitialized => ledger != null;
        internal static void Initialize(ManualLogSource log)
        {
            if (ledger != null || log == null) return;
            logger = log;
            context = System.Threading.SynchronizationContext.Current;
            mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            ledger = new RuntimeHealthLedger(log.SourceName, Write);
        }
        private static ManualLogSource unownedSource;
        internal static void Report(string feature, bool error, string detail)
        {
            RuntimeHealthLedger current = ledger;
            if (current != null) { current.Report(feature, error, detail); return; }
            // Without InitializeDiagnostics (or before it) a failure must still reach the log.
            Failure(UnownedSource(), feature, error, detail);
        }
        private static ManualLogSource UnownedSource()
        {
            ManualLogSource source = unownedSource;
            if (source != null) return source;
            try
            {
                source = BepInEx.Logging.Logger.CreateLogSource(typeof(RuntimeHealth).Assembly.GetName().Name);
                ManualLogSource raced = System.Threading.Interlocked.CompareExchange(ref unownedSource, source, null);
                if (raced != null) { BepInEx.Logging.Logger.Sources.Remove(source); return raced; }
                return source;
            }
            catch { return null; }
        }
        internal static void Failure(ManualLogSource fallback, string feature, bool error, string detail)
        {
            if (fallback == null) return;
            if (ReferenceEquals(fallback, logger)) { ledger?.Report(feature, error, detail); return; }
            // Libraries/offline consumers may use several independent log sources.
            // Do not capture their first logger as the process plugin's logger.
            fallbackLedgers.GetValue(fallback, source => new RuntimeHealthLedger(source.SourceName,
                (level, text) => WriteTo(source, level, text))).Report(feature, error, detail);
        }
        // Existing two-argument logging APIs remain binary compatible. The stack is
        // inspected only when a failure is reported, never during successful gameplay.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static string CallerFeature()
        {
            try
            {
                foreach (var frame in new System.Diagnostics.StackTrace(false).GetFrames())
                {
                    var method = frame.GetMethod(); var type = method?.DeclaringType;
                    string name = type?.FullName;
                    if (name == null || name.StartsWith("System.", StringComparison.Ordinal) ||
                        name.StartsWith("BepInEx.Logging.", StringComparison.Ordinal) ||
                        name.StartsWith("Shared.DebugLogHelper", StringComparison.Ordinal) ||
                        name.StartsWith("Shared.RuntimeHealth", StringComparison.Ordinal) ||
                        name.StartsWith("APIShared.Internal.DebugLogHelper", StringComparison.Ordinal) ||
                        name.StartsWith("APIShared.Internal.RuntimeHealth", StringComparison.Ordinal) ||
                        name == "APIShared.NativeApiLog") continue;
                    return name + "." + method.Name;
                }
            }
            catch { }
            return "unknown-caller";
        }
        internal static void Forward(ManualLogSource target, LogLevel level, string text)
        {
            // Already reported failures passed through a service proxy retain their
            // identity and repeat count. A proxy must not throttle them a second time.
            if (text != null && (text.IndexOf("] " + RuntimeHealthLedger.FailureTag + " mod=", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("] " + RuntimeHealthLedger.WarningTag + " mod=", StringComparison.Ordinal) >= 0))
            { Emit(target, level, text); return; }
            Failure(target, CallerFeature(), (level & (LogLevel.Error | LogLevel.Fatal)) != 0, text);
        }
        private static void Write(LogLevel level, string text) => WriteTo(logger, level, text);
        private static void WriteTo(ManualLogSource target, LogLevel level, string text)
        {
            // Dispatch only emitted failures, without a queue pump or a frame/tick callback.
            // Null context also occurs in offline consumers; retain their ordinary logger path.
            if (context != null && System.Threading.Thread.CurrentThread.ManagedThreadId != mainThread)
            {
                try { context.Post(_ => Emit(target, level, text), null); return; } catch { }
            }
            Emit(target, level, text);
        }
        private static void Emit(ManualLogSource target, LogLevel level, string text)
        { try { target?.Log(level, text); } catch { } }
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
