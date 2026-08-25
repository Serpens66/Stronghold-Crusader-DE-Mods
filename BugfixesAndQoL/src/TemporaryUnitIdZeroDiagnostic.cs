// TEMPORARY DIAGNOSTIC: Remove this file after identifying the TryGetUnitById(0) caller.
using BepInEx.Logging;
using System;
using System.Diagnostics;
using System.Threading;

namespace BugfixesAndQoL
{
    internal sealed class TemporaryUnitIdZeroDiagnostic : ILogListener
    {
        private const string TargetMessage =
            "[GameUnitManagerAPI] [TryGetUnitById] Tried to access unit index that was out of range: [0/10000]";
        private const int MaximumReports = 20;

        private readonly ManualLogSource output;
        private int reportCount;
        private int reporting;
        private bool disposed;

        private TemporaryUnitIdZeroDiagnostic(ManualLogSource output)
        {
            this.output = output ?? throw new ArgumentNullException(nameof(output));
        }

        internal static TemporaryUnitIdZeroDiagnostic Install(ManualLogSource output)
        {
            TemporaryUnitIdZeroDiagnostic diagnostic = new TemporaryUnitIdZeroDiagnostic(output);
            Logger.Listeners.Add(diagnostic);
            Shared.DebugLogHelper.LogInfo(
                output,
                "TEMP Unit-ID-0 caller diagnostic installed; up to 20 matching managed call stacks will be logged.");
            return diagnostic;
        }

        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            if (disposed || eventArgs == null || reportCount >= MaximumReports)
                return;

            string message = eventArgs.Data?.ToString() ?? string.Empty;
            if (!message.Contains(TargetMessage))
                return;

            // Listener dispatch is synchronous, so this captures the managed caller above TryGetUnitById.
            if (Interlocked.Exchange(ref reporting, 1) != 0)
                return;

            try
            {
                int occurrence = Interlocked.Increment(ref reportCount);
                string stackTrace = new StackTrace(skipFrames: 1, fNeedFileInfo: true).ToString();
                Shared.DebugLogHelper.LogWarning(
                    output,
                    $"TEMP Unit-ID-0 caller diagnostic occurrence={occurrence}/{MaximumReports}, " +
                    $"source={eventArgs.Source?.SourceName ?? "unknown"}, original={message}{Environment.NewLine}" +
                    $"Managed stack at matching log emission:{Environment.NewLine}{stackTrace}");
            }
            finally
            {
                Volatile.Write(ref reporting, 0);
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Logger.Listeners.Remove(this);
        }
    }
}
