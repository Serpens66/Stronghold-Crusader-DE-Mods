using System;
using System.Collections.Generic;
using System.Threading;
using BepInEx.Logging;

namespace SerpsModsHost
{
    internal sealed class PackErrorLog : ILogListener
    {
        private const int Capacity = 200;
        private readonly object sync = new object();
        private readonly List<string> errors = new List<string>();
        private readonly HashSet<string> sources;
        private readonly Func<Action, bool> dispatch;
        private readonly Action notify;
        private string latestHookFailure;
        private int notificationQueued;

        internal PackErrorLog(HashSet<string> sources, Func<Action, bool> dispatch, Action notify)
        {
            this.sources = sources;
            this.dispatch = dispatch;
            this.notify = notify;
        }

        internal string LatestHookFailure
        {
            get { lock (sync) return latestHookFailure; }
        }

        internal string Text
        {
            get { lock (sync) return string.Join(Environment.NewLine, errors); }
        }

        internal bool HasErrors
        {
            get { lock (sync) return errors.Count != 0; }
        }

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            if ((eventArgs.Level & (LogLevel.Error | LogLevel.Fatal)) == 0)
                return;
            string source = eventArgs.Source?.SourceName ?? string.Empty;
            if (!sources.Contains(source))
                return;

            string message = Convert.ToString(eventArgs.Data) ?? string.Empty;
            bool hookFailure = message.Contains(Shared.DebugLogHelper.HookInstallFailureTag) ||
                message.Contains("NATIVE_FOREIGN_PATCH:") || message.Contains("NATIVE_FOREIGN_PATCH_LATE:");
            Record($"[{source}] {eventArgs.Level}: {message}", hookFailure);
        }

        internal void Record(string text, bool hookFailure = false)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {text.Trim()}";
            lock (sync)
            {
                if (errors.Count == Capacity)
                    errors.RemoveAt(0);
                errors.Add(line);
                if (hookFailure)
                    latestHookFailure = line;
            }

            if (Interlocked.Exchange(ref notificationQueued, 1) != 0)
                return;
            if (!dispatch(() =>
            {
                Interlocked.Exchange(ref notificationQueued, 0);
                notify();
            }))
                Interlocked.Exchange(ref notificationQueued, 0);
        }

        internal void Clear()
        {
            lock (sync) errors.Clear();
            // The session-wide hook warning still applies after clearing the history.
            notify();
        }

        public void Dispose() { }
    }
}
