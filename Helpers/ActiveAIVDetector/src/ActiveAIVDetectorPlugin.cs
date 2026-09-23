using BepInEx;
using SHCDESE.API.LowLevel;
using System;
using System.IO;

namespace ActiveAIVDetector
{
    [BepInDependency(ScriptExtenderGuid, "2.3.0")]
    [BepInDependency(ApiSharedGuid, "0.3.6")]
    [BepInDependency("APIShared_Serp", "0.3.6")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ActiveAIVDetectorPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string ApiSharedGuid = "APIShared_Serp";

        public const string PluginGuid = "ActiveAIVDetector_Serp";
        public const string PluginName = "Active AIV Detector";
        public const string PluginVersion = "0.10.1";

        // The plugin component is destroyed during startup, so process-lifetime state stays static.
        private static ActiveAIVDetectionRuntime runtime;
        private static bool libraryLoadedHandled;

        // Optional consumers can reuse the installed detour without taking a native dependency.
        public static bool TryRegisterPlacementValidatorObserver(
            Action<ulong, int, int, int, int, int> observer) =>
            runtime != null && runtime.TryRegisterPlacementValidatorObserver(observer);

        private void Awake()
        {
            if (runtime != null)
                return;

            var cellTraceOptions = new OracleCellTraceOptions(
                Config.Bind(
                    "Oracle cell trace",
                    "Enabled",
                    false,
                    "Capture filtered native 100x100 fit grids without changing Vanilla behavior.").Value,
                Config.Bind("Oracle cell trace", "PlayerId", -1).Value,
                Config.Bind("Oracle cell trace", "CandidateId", -1).Value,
                Config.Bind("Oracle cell trace", "Orientation", -1).Value,
                Config.Bind("Oracle cell trace", "KeepX", -1).Value,
                Config.Bind("Oracle cell trace", "KeepY", -1).Value,
                Config.Bind("Oracle cell trace", "MaximumCaptureCount", 256,
                    "Maximum fit-grid captures per map load; lower this if trace files grow too large.").Value,
                Path.Combine(Paths.PluginPath, PluginGuid, "CellTraces"));
            var prebuildTraceOptions = new OraclePrebuildTraceOptions(
                Config.Bind(
                    "Oracle prebuild trace",
                    "Enabled",
                    false,
                    "Trace synchronous ExecuteBuildStep building-grid changes without altering Vanilla behavior.").Value,
                Config.Bind("Oracle prebuild trace", "PlayerId", -1,
                    "Use -1 to capture every AI player in each map load.").Value,
                Config.Bind("Oracle prebuild trace", "MaximumCaptureCount", 8,
                    "Maximum number of player sequences per map load.").Value,
                Path.Combine(Paths.PluginPath, PluginGuid, "PrebuildTraces"));
            runtime = new ActiveAIVDetectionRuntime(
                Logger,
                cellTraceOptions,
                prebuildTraceOptions);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded.");
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogInfo(
                Logger,
                "Plugin component destroyed during startup; keeping the active-AIV hook rooted.");
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (libraryLoadedHandled)
                return;

            try
            {
                bool referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                        Logger,
                        PluginName,
                        requireCurrentVersion: true);
                if (!referenceHashMatches)
                {
                    return;
                }

                runtime.Install(context, referenceHashMatches);
                libraryLoadedHandled = true;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"Active AIV detector initialization failed: {ex}");
            }
        }
    }
}
