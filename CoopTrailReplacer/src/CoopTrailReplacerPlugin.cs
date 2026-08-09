using BepInEx;
using SHCDESE.API.LowLevel;
using System;
using System.IO;

namespace CoopTrailReplacer
{
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class CoopTrailReplacerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "CoopTrailReplacer_Serp";
        public const string PluginName = "Coop Trail Replacer";
        public const string PluginVersion = "1.1.0";

        private static CoopTrailReplacerRuntime runtime;

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(Logger, PluginName + " " + PluginVersion + " loaded.");
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private void OnLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            try
            {
                CrusaderLibrary.Instance.LibraryLoaded -= OnLibraryLoaded;
                Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName);
                string pluginRoot = Path.GetDirectoryName(Info.Location);
                runtime = new CoopTrailReplacerRuntime(Logger, pluginRoot);
                runtime.Initialize();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, "CoopTrailReplacer initialization failed: " + ex);
            }
        }

        private void OnDestroy()
        {
            // The BepInEx manager object is destroyed during startup; runtime hooks must survive it.
            Shared.DebugLogHelper.LogDebug(Logger, "CoopTrailReplacer OnDestroy called; keeping process-lifetime runtime active.");
        }

        private void OnApplicationQuit()
        {
            runtime?.Dispose();
            runtime = null;
        }
    }
}
