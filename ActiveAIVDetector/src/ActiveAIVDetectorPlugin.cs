using BepInEx;
using SHCDESE.API.LowLevel;
using System;
using System.IO;

namespace ActiveAIVDetector
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ActiveAIVDetectorPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "ActiveAIVDetector_Serp";
        public const string PluginName = "Active AIV Detector";
        public const string PluginVersion = "0.9.1";

        // The plugin component is destroyed during startup, so process-lifetime state stays static.
        private static ActiveAIVDetectionRuntime runtime;
        private static bool libraryLoadedHandled;

        private void Awake()
        {
            if (runtime != null)
                return;

            var cellTraceOptions = new OracleCellTraceOptions(
                Config.Bind(
                    "Oracle cell trace",
                    "Enabled",
                    false,
                    "Capture one filtered native 100x100 fit grid without changing Vanilla behavior.").Value,
                Config.Bind("Oracle cell trace", "PlayerId", 2).Value,
                Config.Bind("Oracle cell trace", "CandidateId", 0).Value,
                Config.Bind("Oracle cell trace", "Orientation", 0).Value,
                Config.Bind("Oracle cell trace", "KeepX", 363).Value,
                Config.Bind("Oracle cell trace", "KeepY", 428).Value,
                Config.Bind("Oracle cell trace", "MaximumCaptureCount", 1).Value,
                Path.Combine(Paths.PluginPath, PluginGuid, "CellTraces"));
            runtime = new ActiveAIVDetectionRuntime(Logger, cellTraceOptions);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded.");
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogInfo(
                Logger,
                "Plugin component destroyed during startup; keeping the active-AIV hook rooted.");
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (libraryLoadedHandled)
                return;

            try
            {
                runtime.Install(libraryHandle, memory);
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
