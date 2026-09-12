using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CrusaderDE;
using StartupPerformanceDiagnostic.Patcher;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StartupPerformanceDiagnostic
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class StartupPerformanceDiagnosticPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "StartupPerformanceDiagnostic_Serp";
        public const string PluginName = "Startup Performance Diagnostic";
        public const string PluginVersion = "0.1.0";

        private static StartupPerformanceDiagnosticPlugin instance;
        private static ManualLogSource rootedLog;
        private static bool frontendReadyObserved;
        private static bool reportEmitted;
        private static bool readinessFailureLogged;

        private void Awake()
        {
            instance = this;
            rootedLog = Logger;
            Application.onBeforeRender += ObserveRenderedFrontend;
        }

        private void Update()
        {
            if (reportEmitted || frontendReadyObserved)
                return;

            try
            {
                MainViewModel viewModel = MainViewModel.instance;
                frontendReadyObserved = viewModel != null &&
                                        viewModel.FrontEndMenu != null &&
                                        viewModel.Show_FrontMenus &&
                                        !FrontendMenus.loadingStill;
            }
            catch (Exception ex)
            {
                if (readinessFailureLogged)
                    return;
                readinessFailureLogged = true;
                rootedLog.LogWarning("Frontend readiness check failed once: " + ex);
            }
        }

        private static void ObserveRenderedFrontend()
        {
            if (!frontendReadyObserved || reportEmitted)
                return;

            reportEmitted = true;
            long frontendRenderedTimestamp = Stopwatch.GetTimestamp();
            try
            {
                WriteReport(frontendRenderedTimestamp);
            }
            catch (Exception ex)
            {
                rootedLog?.LogError("Startup timing report failed: " + ex);
            }
        }

        private static void WriteReport(long frontendRenderedTimestamp)
        {
            StartupTimingSnapshot snapshot = StartupTimingBridge.Capture();
            TimelineEvent[] events = snapshot.Events
                .Select(item => new TimelineEvent(item.Timestamp, item.Message))
                .ToArray();
            StartupTimingAnalysis analysis = TimingAnalysis.Analyze(
                events,
                snapshot.ProcessOriginTimestamp,
                snapshot.AttachedTimestamp,
                frontendRenderedTimestamp);
            foreach (string warning in snapshot.Warnings)
                analysis.Warnings.Add(warning);

            PluginDescriptor[] descriptors = Chainloader.PluginInfos
                .Select(pair => new PluginDescriptor(pair.Key, pair.Value))
                .ToArray();
            var mappingWarnings = new List<string>();
            List<ResolvedPluginTiming> resolved = ResolvePlugins(analysis.Plugins, descriptors, mappingWarnings);
            analysis.Warnings.AddRange(mappingWarnings);

            long totalTicks = Math.Max(0, frontendRenderedTimestamp - analysis.ProcessOriginTimestamp);
            long pluginTotalTicks = analysis.PluginTotalTicks;
            long postChainloaderTicks = analysis.ChainloaderCompleteTimestamp.HasValue
                ? Math.Max(0, frontendRenderedTimestamp - analysis.ChainloaderCompleteTimestamp.Value)
                : 0;

            LogLine("============================================================");
            LogLine("STARTUP PERFORMANCE REPORT");
            LogLine("Clock: Stopwatch; process-origin estimate uses Process.StartTime.");
            LogDuration("TOTAL process start -> first rendered frontend", totalTicks, totalTicks);
            LogLine("-------------------- PHASES --------------------------------");
            LogDuration("Early process/Unity/BepInEx before profiler attach",
                analysis.AttachedTimestamp - analysis.ProcessOriginTimestamp, totalTicks);
            LogOptionalDuration("BepInEx preloader remainder after attach",
                analysis.AttachedTimestamp, analysis.PreloaderFinishedTimestamp, totalTicks);
            LogOptionalDuration("Chainloader initialization",
                analysis.PreloaderFinishedTimestamp, analysis.ChainloaderStartedTimestamp, totalTicks);
            LogOptionalDuration("Plugin discovery/dependency sorting",
                analysis.ChainloaderStartedTimestamp, analysis.FirstPluginTimestamp, totalTicks);
            LogOptionalDuration("Synchronous plugin loading phase",
                analysis.FirstPluginTimestamp, analysis.ChainloaderCompleteTimestamp, totalTicks);
            LogDuration("Post-chainloader/game work -> rendered frontend", postChainloaderTicks, totalTicks);

            long observableCoreTicks = CalculateObservableCoreTicks(analysis);
            LogDuration("Observable BepInEx core after profiler attach (excludes plugin windows)",
                observableCoreTicks, totalTicks);

            LogLine("-------------------- PLUGINS -------------------------------");
            LogLine("Each window starts at BepInEx 'Loading [...]' and ends at the next loading marker.");
            for (int index = 0; index < resolved.Count; index++)
            {
                ResolvedPluginTiming item = resolved[index];
                string identity = item.Descriptor == null
                    ? "unresolved | " + item.Window.Label
                    : item.Descriptor.Guid + " | " + item.Descriptor.Name + " " + item.Descriptor.Version;
                string location = item.Descriptor == null ? "unknown" : item.Descriptor.Location;
                LogLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0,2}. {1} | {2:F3} ms | {3:F2}% total | {4:F2}% plugin phase | path={5}",
                    index + 1,
                    identity,
                    TimingAnalysis.Milliseconds(item.Window.DurationTicks, snapshot.Frequency),
                    TimingAnalysis.Percentage(item.Window.DurationTicks, totalTicks),
                    TimingAnalysis.Percentage(item.Window.DurationTicks, pluginTotalTicks),
                    location));
            }

            LogLine("-------------------- SUMMARY -------------------------------");
            LogDuration("Sum of synchronous plugin windows", pluginTotalTicks, totalTicks);
            ResolvedPluginTiming scriptExtender = resolved.FirstOrDefault(
                item => string.Equals(item.Descriptor?.Guid, "000shcdese", StringComparison.Ordinal));
            if (scriptExtender != null)
                LogDuration("SHCDE-SE synchronous loading", scriptExtender.Window.DurationTicks, totalTicks);
            else
                analysis.Warnings.Add("SHCDE-SE timing could not be mapped to GUID 000shcdese.");
            LogDuration("REST after Chainloader completion", postChainloaderTicks, totalTicks);
            LogLine("Plugin windows represent synchronous assembly load/constructor/Awake work; deferred work is part of REST.");

            if (analysis.Warnings.Count > 0)
            {
                LogLine("-------------------- WARNINGS ------------------------------");
                foreach (string warning in analysis.Warnings.Distinct(StringComparer.Ordinal))
                    LogLine("WARNING: " + warning);
            }
            LogLine("============================================================");
        }

        private static long CalculateObservableCoreTicks(StartupTimingAnalysis analysis)
        {
            long end = analysis.ChainloaderCompleteTimestamp ?? analysis.FrontendRenderedTimestamp;
            long elapsed = Math.Max(0, end - analysis.AttachedTimestamp);
            return Math.Max(0, elapsed - analysis.PluginTotalTicks);
        }

        private static List<ResolvedPluginTiming> ResolvePlugins(
            IEnumerable<PluginTimingWindow> windows,
            IEnumerable<PluginDescriptor> descriptors,
            ICollection<string> warnings)
        {
            var remaining = descriptors.ToList();
            var result = new List<ResolvedPluginTiming>();
            foreach (PluginTimingWindow window in windows)
            {
                List<PluginDescriptor> matches = remaining
                    .Where(item => string.Equals(item.LoadLabel, window.Label, StringComparison.Ordinal))
                    .ToList();
                PluginDescriptor descriptor = matches.Count == 1 ? matches[0] : null;
                if (matches.Count == 0)
                    warnings.Add("No loaded-plugin metadata matched timing label '" + window.Label + "'.");
                else if (matches.Count > 1)
                    warnings.Add("Multiple loaded plugins matched timing label '" + window.Label + "'.");
                if (descriptor != null)
                    remaining.Remove(descriptor);
                result.Add(new ResolvedPluginTiming(window, descriptor));
            }
            return result;
        }

        private static void LogOptionalDuration(string label, long? start, long? end, long totalTicks)
        {
            if (!start.HasValue || !end.HasValue)
            {
                LogLine(label + ": unavailable");
                return;
            }
            LogDuration(label, Math.Max(0, end.Value - start.Value), totalTicks);
        }

        private static void LogOptionalDuration(string label, long start, long? end, long totalTicks)
        {
            LogOptionalDuration(label, (long?)start, end, totalTicks);
        }

        private static void LogDuration(string label, long ticks, long totalTicks)
        {
            StartupTimingSnapshot snapshot = StartupTimingBridge.Capture();
            LogLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0}: {1:F3} ms ({2:F2}% of total)",
                label,
                TimingAnalysis.Milliseconds(ticks, snapshot.Frequency),
                TimingAnalysis.Percentage(ticks, totalTicks)));
        }

        private static void LogLine(string message)
        {
            rootedLog.LogInfo("[StartupTiming] " + message);
        }

        private sealed class PluginDescriptor
        {
            internal PluginDescriptor(string guid, PluginInfo info)
            {
                Guid = guid ?? string.Empty;
                Name = info?.Metadata?.Name ?? string.Empty;
                Version = info?.Metadata?.Version?.ToString() ?? string.Empty;
                Location = info == null || string.IsNullOrEmpty(info.Location)
                    ? "unknown"
                    : MakeDisplayPath(info.Location);
                LoadLabel = Name + " " + Version;
            }

            internal string Guid { get; }
            internal string Name { get; }
            internal string Version { get; }
            internal string Location { get; }
            internal string LoadLabel { get; }

            private static string MakeDisplayPath(string location)
            {
                try
                {
                    string pluginRoot = Path.GetFullPath(Paths.PluginPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    string full = Path.GetFullPath(location);
                    return full.StartsWith(pluginRoot, StringComparison.OrdinalIgnoreCase)
                        ? full.Substring(pluginRoot.Length)
                        : full;
                }
                catch
                {
                    return location;
                }
            }
        }

        private sealed class ResolvedPluginTiming
        {
            internal ResolvedPluginTiming(PluginTimingWindow window, PluginDescriptor descriptor)
            {
                Window = window;
                Descriptor = descriptor;
            }

            internal PluginTimingWindow Window { get; }
            internal PluginDescriptor Descriptor { get; }
        }
    }
}
