using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using BepInEx.Logging;

namespace UiAcceptance
{
    // ACCEPT result lines plus a bounded in-process copy of every BepInEx log event, so steps can
    // assert markers of other mods without reading the (overwritten) log file.
    internal static class AcceptLog
    {
        private const int Capacity = 200000;
        private static readonly object sync = new object();
        private static readonly List<Entry> entries = new List<Entry>();
        private static long first;
        private static ManualLogSource log;
        internal static string RunId = "none";

        internal struct Entry { internal long Index; internal LogLevel Level; internal string Source; internal string Text; }

        internal static void Attach(ManualLogSource logger)
        {
            log = logger;
            // Plugins loaded earlier already logged startup markers (e.g. hook installation); import the
            // disk log written so far once, then follow live events.
            try
            {
                foreach (ILogListener listener in Logger.Listeners)
                    if (listener is DiskLogListener disk) disk.LogWriter?.Flush();
                string path = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "LogOutput.log");
                using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                using (var reader = new System.IO.StreamReader(stream))
                    for (string line; (line = reader.ReadLine()) != null;)
                        lock (sync) entries.Add(new Entry { Index = first + entries.Count, Level = LogLevel.Info, Source = "startup-log", Text = line });
            }
            catch (Exception error) { logger.LogWarning(Stamp("startup log import failed: " + error.Message)); }
            Logger.Listeners.Add(new Listener());
        }

        internal static long Position { get { lock (sync) return first + entries.Count; } }

        // The runner's own STEP/ACCEPT lines echo the searched patterns and never count as evidence.
        private static bool IsEcho(Entry entry) =>
            string.Equals(entry.Source, UiAcceptancePlugin.Name, StringComparison.Ordinal) &&
            (entry.Text.Contains(" STEP ") || entry.Text.Contains(" ACCEPT run="));

        internal static int Count(long from, Regex pattern)
        {
            int count = 0;
            lock (sync)
                for (int i = (int)Math.Max(0, from - first); i < entries.Count; i++)
                    if (!IsEcho(entries[i]) && pattern.IsMatch(entries[i].Text)) count++;
            return count;
        }

        // First capture group of every match, in log order.
        internal static List<long> Numbers(long from, Regex pattern)
        {
            var values = new List<long>();
            lock (sync)
                for (int i = (int)Math.Max(0, from - first); i < entries.Count; i++)
                {
                    if (IsEcho(entries[i])) continue;
                    Match match = pattern.Match(entries[i].Text);
                    if (match.Success && match.Groups.Count > 1 && long.TryParse(match.Groups[1].Value, out long value)) values.Add(value);
                }
            return values;
        }

        internal static string Last(long from, Regex pattern)
        {
            lock (sync)
                for (int i = entries.Count - 1; i >= Math.Max(0, from - first); i--)
                    if (pattern.IsMatch(entries[i].Text)) return entries[i].Text;
            return null;
        }

        // Error/Fatal events of other sources since a position; own FAIL lines are excluded.
        internal static List<string> Errors(long from)
        {
            var found = new List<string>();
            lock (sync)
                for (int i = (int)Math.Max(0, from - first); i < entries.Count; i++)
                    if ((entries[i].Level & (LogLevel.Error | LogLevel.Fatal)) != 0 &&
                        !string.Equals(entries[i].Source, UiAcceptancePlugin.Name, StringComparison.Ordinal))
                        found.Add(entries[i].Source + ": " + entries[i].Text);
            return found;
        }

        internal static string Stamp(string message) =>
            DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + message;
        internal static void Info(string message) => log?.LogInfo(Stamp(message));
        internal static void Warn(string message) => log?.LogWarning(Stamp(message));
        internal static void Result(string step, bool pass, string detail) =>
            log?.LogMessage(Stamp("ACCEPT run=" + RunId + " step=" + step + " " + (pass ? "PASS" : "FAIL") + " " + detail));

        private sealed class Listener : ILogListener
        {
            public void LogEvent(object sender, LogEventArgs eventArgs)
            {
                var entry = new Entry { Level = eventArgs.Level, Source = eventArgs.Source?.SourceName, Text = eventArgs.Data?.ToString() ?? string.Empty };
                lock (sync)
                {
                    entry.Index = first + entries.Count;
                    entries.Add(entry);
                    if (entries.Count > Capacity) { entries.RemoveRange(0, Capacity / 4); first += Capacity / 4; }
                }
            }
            public void Dispose() { }
        }
    }
}
