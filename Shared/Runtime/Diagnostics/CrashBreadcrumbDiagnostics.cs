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
            bool enabled = DebugLogHelper.CrashBreadcrumbsEnabled;
            if (!enabled)
                return;
            try
            {
                recorder = new CrashBreadcrumbRecorder(
                    enabled, BepInEx.Paths.BepInExRootPath, pluginGuid, pluginName, pluginVersion,
                    null, null, null, error => ReportPersistenceFailure(log, pluginGuid, error));
                Application.quitting += MarkCleanShutdown;
            }
            catch (Exception ex)
            {
                RuntimeHealth.Failure(log, "crash-breadcrumb-initialization", true,
                    "Crash breadcrumb initialization failed; crash recording is unavailable. mod=" + pluginGuid + "; " + ex);
            }
        }

        internal static void ReportPersistenceFailure(ManualLogSource log, string pluginGuid, Exception error) =>
            RuntimeHealth.Failure(log, "crash-breadcrumb-persistence", true,
                "Crash breadcrumb disk recording failed and is disabled for this process; no further writes will be attempted. mod=" +
                pluginGuid + "; " + error);

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
