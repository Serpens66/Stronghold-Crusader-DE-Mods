using BepInEx.Logging;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace StartupPerformanceDiagnostic.Patcher
{
    public static class StartupTimingPatcher
    {
        public static IEnumerable<string> TargetDLLs => Array.Empty<string>();

        public static void Initialize()
        {
            StartupTimingBridge.Initialize();
        }

        public static void Patch(AssemblyDefinition assembly)
        {
        }
    }

    public sealed class StartupTimingEvent
    {
        internal StartupTimingEvent(long timestamp, string source, string message)
        {
            Timestamp = timestamp;
            Source = source ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public long Timestamp { get; }
        public string Source { get; }
        public string Message { get; }
    }

    public sealed class StartupTimingSnapshot
    {
        internal StartupTimingSnapshot(
            long frequency,
            long processOriginTimestamp,
            long attachedTimestamp,
            StartupTimingEvent[] events,
            string[] warnings)
        {
            Frequency = frequency;
            ProcessOriginTimestamp = processOriginTimestamp;
            AttachedTimestamp = attachedTimestamp;
            Events = events;
            Warnings = warnings;
        }

        public long Frequency { get; }
        public long ProcessOriginTimestamp { get; }
        public long AttachedTimestamp { get; }
        public StartupTimingEvent[] Events { get; }
        public string[] Warnings { get; }
    }

    public static class StartupTimingBridge
    {
        private const string BepInExSource = "BepInEx";
        private static readonly object Gate = new object();
        private static readonly List<StartupTimingEvent> Events = new List<StartupTimingEvent>();
        private static readonly List<string> Warnings = new List<string>();
        private static StartupTimingListener rootedListener;
        private static long processOriginTimestamp;
        private static long attachedTimestamp;
        private static bool initialized;

        public static void Initialize()
        {
            lock (Gate)
            {
                if (initialized)
                    return;

                attachedTimestamp = Stopwatch.GetTimestamp();
                processOriginTimestamp = EstimateProcessOrigin(attachedTimestamp);
                rootedListener = new StartupTimingListener();
                Logger.Listeners.Add(rootedListener);
                initialized = true;
            }
        }

        public static StartupTimingSnapshot Capture()
        {
            lock (Gate)
            {
                return new StartupTimingSnapshot(
                    Stopwatch.Frequency,
                    processOriginTimestamp,
                    attachedTimestamp,
                    Events.ToArray(),
                    Warnings.ToArray());
            }
        }

        private static long EstimateProcessOrigin(long nowTimestamp)
        {
            try
            {
                double elapsedSeconds = (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
                if (elapsedSeconds < 0 || elapsedSeconds > 3600)
                {
                    Warnings.Add("Process start time was outside the expected range; total launch time starts at profiler attachment.");
                    return nowTimestamp;
                }

                double elapsedTicks = elapsedSeconds * Stopwatch.Frequency;
                if (elapsedTicks > long.MaxValue)
                    return nowTimestamp;
                return nowTimestamp - (long)Math.Round(elapsedTicks, MidpointRounding.AwayFromZero);
            }
            catch (Exception ex)
            {
                Warnings.Add("Process start time could not be read: " + ex.GetType().Name + ".");
                return nowTimestamp;
            }
        }

        private static bool IsRelevant(string source, string message)
        {
            if (!string.Equals(source, BepInExSource, StringComparison.Ordinal))
                return false;
            return string.Equals(message, "Preloader finished", StringComparison.Ordinal) ||
                   string.Equals(message, "Chainloader ready", StringComparison.Ordinal) ||
                   string.Equals(message, "Chainloader started", StringComparison.Ordinal) ||
                   string.Equals(message, "Chainloader startup complete", StringComparison.Ordinal) ||
                   message.StartsWith("Loading [", StringComparison.Ordinal) ||
                   message.EndsWith(" plugins to load", StringComparison.Ordinal) ||
                   message.EndsWith(" plugin to load", StringComparison.Ordinal);
        }

        private sealed class StartupTimingListener : ILogListener
        {
            public void LogEvent(object sender, LogEventArgs eventArgs)
            {
                string source = eventArgs.Source?.SourceName ?? string.Empty;
                string message = Convert.ToString(eventArgs.Data, CultureInfo.InvariantCulture) ?? string.Empty;
                if (!IsRelevant(source, message))
                    return;

                long timestamp = Stopwatch.GetTimestamp();
                lock (Gate)
                    Events.Add(new StartupTimingEvent(timestamp, source, message));
            }

            public void Dispose()
            {
                // Required by ILogListener. The listener is intentionally process-lifetime rooted.
            }
        }
    }
}
