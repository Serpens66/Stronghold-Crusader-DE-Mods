using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace ActiveAIVDetector
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ActiveAIVDetectorPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "ActiveAIVDetector_Serp";
        public const string PluginName = "Active AIV Detector";
        public const string PluginVersion = "0.7.0";

        // The plugin component is destroyed during startup, so process-lifetime state stays static.
        private static ActiveAIVDetectionRuntime runtime;
        private static bool libraryLoadedHandled;

        private void Awake()
        {
            if (runtime != null)
                return;

            runtime = new ActiveAIVDetectionRuntime(Logger);
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
