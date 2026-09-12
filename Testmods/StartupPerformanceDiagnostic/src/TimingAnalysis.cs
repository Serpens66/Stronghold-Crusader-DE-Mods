using System;
using System.Collections.Generic;
using System.Linq;

namespace StartupPerformanceDiagnostic
{
    internal sealed class TimelineEvent
    {
        internal TimelineEvent(long timestamp, string message, string source = "")
        {
            Timestamp = timestamp;
            Message = message ?? string.Empty;
            Source = source ?? string.Empty;
        }

        internal long Timestamp { get; }
        internal string Message { get; }
        internal string Source { get; }
    }

    internal sealed class DetailTimingWindow
    {
        internal DetailTimingWindow(string component, string span, string parent, string source, long started, long completed)
        {
            Component = component;
            Span = span;
            Parent = parent;
            Source = source;
            StartedTimestamp = started;
            CompletedTimestamp = completed;
            ExclusiveTicks = DurationTicks;
        }

        internal string Component { get; }
        internal string Span { get; }
        internal string Parent { get; }
        internal string Source { get; }
        internal long StartedTimestamp { get; }
        internal long CompletedTimestamp { get; }
        internal long DurationTicks => Math.Max(0, CompletedTimestamp - StartedTimestamp);
        internal long ExclusiveTicks { get; set; }
    }

    internal sealed class PluginTimingWindow
    {
        internal PluginTimingWindow(string label, long startedTimestamp, long completedTimestamp)
        {
            Label = label;
            StartedTimestamp = startedTimestamp;
            CompletedTimestamp = completedTimestamp;
        }

        internal string Label { get; }
        internal long StartedTimestamp { get; }
        internal long CompletedTimestamp { get; }
        internal long DurationTicks => Math.Max(0, CompletedTimestamp - StartedTimestamp);
    }

    internal sealed class StartupTimingAnalysis
    {
        internal long ProcessOriginTimestamp { get; set; }
        internal long AttachedTimestamp { get; set; }
        internal long? PreloaderFinishedTimestamp { get; set; }
        internal long? ChainloaderReadyTimestamp { get; set; }
        internal long? ChainloaderStartedTimestamp { get; set; }
        internal long? FirstPluginTimestamp { get; set; }
        internal long? ChainloaderCompleteTimestamp { get; set; }
        internal long FrontendRenderedTimestamp { get; set; }
        internal List<PluginTimingWindow> Plugins { get; } = new List<PluginTimingWindow>();
        internal List<DetailTimingWindow> Details { get; } = new List<DetailTimingWindow>();
        internal List<string> Warnings { get; } = new List<string>();

        internal long PluginTotalTicks => Plugins.Sum(plugin => plugin.DurationTicks);
    }

    internal static class TimingAnalysis
    {
        private const string LoadingPrefix = "Loading [";
        private const string DetailPrefix = "[StartupTiming.Detail] ";

        internal static StartupTimingAnalysis Analyze(
            IEnumerable<TimelineEvent> sourceEvents,
            long processOriginTimestamp,
            long attachedTimestamp,
            long frontendRenderedTimestamp)
        {
            var result = new StartupTimingAnalysis
            {
                ProcessOriginTimestamp = processOriginTimestamp,
                AttachedTimestamp = attachedTimestamp,
                FrontendRenderedTimestamp = frontendRenderedTimestamp
            };

            TimelineEvent[] events = (sourceEvents ?? Enumerable.Empty<TimelineEvent>())
                .OrderBy(item => item.Timestamp)
                .ToArray();
            PluginTimingWindow openPlugin = null;
            var openDetails = new Dictionary<string, TimelineEvent>(StringComparer.Ordinal);

            foreach (TimelineEvent item in events)
            {
                if (item.Message.StartsWith(DetailPrefix, StringComparison.Ordinal))
                    ProcessDetailMarker(item, openDetails, result);
                else if (string.Equals(item.Message, "Preloader finished", StringComparison.Ordinal))
                    result.PreloaderFinishedTimestamp = SetOnce(result.PreloaderFinishedTimestamp, item.Timestamp, "Preloader finished", result.Warnings, false);
                else if (string.Equals(item.Message, "Chainloader ready", StringComparison.Ordinal))
                    result.ChainloaderReadyTimestamp = SetOnce(result.ChainloaderReadyTimestamp, item.Timestamp, "Chainloader ready", result.Warnings);
                else if (string.Equals(item.Message, "Chainloader started", StringComparison.Ordinal))
                    result.ChainloaderStartedTimestamp = SetOnce(result.ChainloaderStartedTimestamp, item.Timestamp, "Chainloader started", result.Warnings);
                else if (item.Message.StartsWith(LoadingPrefix, StringComparison.Ordinal))
                {
                    if (openPlugin != null)
                        result.Plugins.Add(new PluginTimingWindow(openPlugin.Label, openPlugin.StartedTimestamp, item.Timestamp));

                    string label = ParsePluginLabel(item.Message);
                    if (label.Length == 0)
                        result.Warnings.Add("A plugin loading marker had no parseable label: " + item.Message);
                    openPlugin = new PluginTimingWindow(label, item.Timestamp, item.Timestamp);
                    if (!result.FirstPluginTimestamp.HasValue)
                        result.FirstPluginTimestamp = item.Timestamp;
                }
                else if (string.Equals(item.Message, "Chainloader startup complete", StringComparison.Ordinal))
                {
                    result.ChainloaderCompleteTimestamp = SetOnce(result.ChainloaderCompleteTimestamp, item.Timestamp, "Chainloader startup complete", result.Warnings);
                    if (openPlugin != null)
                    {
                        result.Plugins.Add(new PluginTimingWindow(openPlugin.Label, openPlugin.StartedTimestamp, item.Timestamp));
                        openPlugin = null;
                    }
                }
            }

            if (openPlugin != null)
                result.Warnings.Add("The final plugin timing window is incomplete because Chainloader startup completion was not observed.");
            foreach (string key in openDetails.Keys.OrderBy(value => value, StringComparer.Ordinal))
                result.Warnings.Add("Detail span did not complete: " + key + ".");
            CalculateExclusiveDetailDurations(result.Details);
            ValidateOrdering(result);
            return result;
        }

        internal static double Milliseconds(long ticks, long frequency) =>
            frequency <= 0 ? 0 : Math.Max(0, ticks) * 1000.0 / frequency;

        internal static double Percentage(long partTicks, long totalTicks) =>
            totalTicks <= 0 ? 0 : Math.Max(0, partTicks) * 100.0 / totalTicks;

        private static string ParsePluginLabel(string message)
        {
            if (!message.StartsWith(LoadingPrefix, StringComparison.Ordinal) || !message.EndsWith("]", StringComparison.Ordinal))
                return string.Empty;
            return message.Substring(LoadingPrefix.Length, message.Length - LoadingPrefix.Length - 1);
        }

        private static void ProcessDetailMarker(
            TimelineEvent item,
            IDictionary<string, TimelineEvent> open,
            StartupTimingAnalysis result)
        {
            string[] fields = item.Message.Substring(DetailPrefix.Length).Split('|');
            if (fields.Length != 4 ||
                (fields[0] != "BEGIN" && fields[0] != "END") ||
                fields[1].Length == 0 || fields[2].Length == 0)
            {
                result.Warnings.Add("Malformed detail marker: " + item.Message);
                return;
            }

            string key = fields[1] + "|" + fields[2];
            if (fields[0] == "BEGIN")
            {
                if (open.ContainsKey(key))
                {
                    result.Warnings.Add("Duplicate detail BEGIN marker: " + key + ".");
                    return;
                }
                open[key] = item;
                return;
            }

            if (!open.TryGetValue(key, out TimelineEvent begin))
            {
                result.Warnings.Add("Detail END marker had no matching BEGIN: " + key + ".");
                return;
            }
            open.Remove(key);
            string[] beginFields = begin.Message.Substring(DetailPrefix.Length).Split('|');
            if (!string.Equals(beginFields[3], fields[3], StringComparison.Ordinal))
                result.Warnings.Add("Detail parent changed between BEGIN and END: " + key + ".");
            if (!string.Equals(begin.Source, item.Source, StringComparison.Ordinal))
                result.Warnings.Add("Detail source changed between BEGIN and END: " + key + ".");
            result.Details.Add(new DetailTimingWindow(
                fields[1], fields[2], beginFields[3], begin.Source,
                begin.Timestamp, item.Timestamp));
        }

        private static void CalculateExclusiveDetailDurations(IList<DetailTimingWindow> details)
        {
            foreach (DetailTimingWindow parent in details)
            {
                long childTicks = details
                    .Where(child => string.Equals(child.Component, parent.Component, StringComparison.Ordinal) &&
                        string.Equals(child.Parent, parent.Span, StringComparison.Ordinal) &&
                        child.StartedTimestamp >= parent.StartedTimestamp &&
                        child.CompletedTimestamp <= parent.CompletedTimestamp)
                    .Sum(child => child.DurationTicks);
                parent.ExclusiveTicks = Math.Max(0, parent.DurationTicks - childTicks);
            }
        }

        private static long? SetOnce(
            long? target,
            long value,
            string marker,
            ICollection<string> warnings,
            bool warnOnDuplicate = true)
        {
            if (target.HasValue)
            {
                if (warnOnDuplicate)
                    warnings.Add("Duplicate marker observed: " + marker + ".");
                return target;
            }
            return value;
        }

        private static void ValidateOrdering(StartupTimingAnalysis result)
        {
            if (!result.PreloaderFinishedTimestamp.HasValue)
                result.Warnings.Add("Preloader completion was not observed.");
            if (!result.ChainloaderReadyTimestamp.HasValue)
                result.Warnings.Add("Chainloader readiness was not observed.");
            if (!result.ChainloaderStartedTimestamp.HasValue)
                result.Warnings.Add("Chainloader start was not observed.");
            if (!result.FirstPluginTimestamp.HasValue)
                result.Warnings.Add("No plugin loading marker was observed.");
            if (!result.ChainloaderCompleteTimestamp.HasValue)
                result.Warnings.Add("Chainloader completion was not observed.");

            var ordered = new[]
            {
                result.AttachedTimestamp,
                result.PreloaderFinishedTimestamp ?? result.AttachedTimestamp,
                result.ChainloaderReadyTimestamp ?? result.AttachedTimestamp,
                result.ChainloaderStartedTimestamp ?? result.AttachedTimestamp,
                result.FirstPluginTimestamp ?? result.AttachedTimestamp,
                result.ChainloaderCompleteTimestamp ?? result.FrontendRenderedTimestamp,
                result.FrontendRenderedTimestamp
            };
            for (int index = 1; index < ordered.Length; index++)
            {
                if (ordered[index] < ordered[index - 1])
                {
                    result.Warnings.Add("Startup markers were observed out of chronological order.");
                    break;
                }
            }
        }
    }
}
