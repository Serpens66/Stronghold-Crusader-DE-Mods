using BepInEx.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Spectator
{
    internal sealed class TimestampLog
    {
        private readonly ManualLogSource source;
        private readonly int mainThread = Thread.CurrentThread.ManagedThreadId;
        private readonly ConcurrentQueue<Tuple<LogLevel, string>> pending = new ConcurrentQueue<Tuple<LogLevel, string>>();
        private int queued;
        internal TimestampLog(ManualLogSource source) { this.source = source; }
        private static string Stamp(object value) => "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + value;
        internal bool IsMainThread => Thread.CurrentThread.ManagedThreadId == mainThread;
        private void Write(LogLevel level, object value)
        {
            string text = Stamp(value);
            if (IsMainThread) source.Log(level, text);
            else if (Interlocked.Increment(ref queued) <= 1024) pending.Enqueue(Tuple.Create(level, text));
            else Interlocked.Decrement(ref queued);
        }
        internal void Flush()
        {
            if (!IsMainThread) return;
            for (int count = 0; count < 256 && pending.TryDequeue(out var entry); count++)
            { Interlocked.Decrement(ref queued); source.Log(entry.Item1, entry.Item2); }
        }
        internal void LogInfo(object value) => Write(LogLevel.Info, value);
        internal void LogWarning(object value) => Write(LogLevel.Warning, value);
        internal void LogError(object value) => Write(LogLevel.Error, value);
    }
}
