using BepInEx.Logging;
using System;
using UnityEngine;

namespace Shared
{
    internal static class CrashBreadcrumbDiagnostics
    {
        private static CrashBreadcrumbRecorder recorder;
        private static bool initialized;

        internal static bool IsEnabled => recorder?.IsEnabled == true;

        internal static void Initialize(
            ManualLogSource log,
            string pluginGuid,
            string pluginName,
            string pluginVersion)
        {
            if (initialized)
                return;

            initialized = true;
            bool enabled = DebugLogHelper.IsDebugEnabled();
            recorder = new CrashBreadcrumbRecorder(
                enabled,
                BepInEx.Paths.BepInExRootPath,
                pluginGuid,
                pluginName,
                pluginVersion,
                message => DebugLogHelper.LogDebug(log, message));
            if (!enabled)
                return;

            Application.quitting += MarkCleanShutdown;
            DebugLogHelper.LogDebug(
                log,
                $"Crash breadcrumb diagnostics enabled: plugin={pluginGuid}, " +
                "ringCapacity=256, snapshotIntervalSeconds=1, retainedSessions=3.");
        }

        internal static CrashBreadcrumbScope Enter(
            string operation,
            long value1 = 0,
            long value2 = 0,
            long value3 = 0,
            long value4 = 0) =>
            recorder?.Enter(operation, value1, value2, value3, value4) ??
            default(CrashBreadcrumbScope);

        internal static void Record(
            string operation,
            long value1 = 0,
            long value2 = 0,
            long value3 = 0,
            long value4 = 0,
            int outcome = 0) =>
            recorder?.Record(operation, value1, value2, value3, value4, outcome);

        internal static bool ShouldLogUnexpected(string signature) =>
            recorder?.TryRegisterUnexpected(signature) ?? true;

        private static void MarkCleanShutdown() => recorder?.MarkCleanShutdown();
    }
}
