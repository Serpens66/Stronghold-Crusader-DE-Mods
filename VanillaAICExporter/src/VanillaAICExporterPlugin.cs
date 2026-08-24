using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace VanillaAICExporter
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class VanillaAICExporterPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "VanillaAICExporter_Serp";
        public const string PluginName = "Vanilla AIC Exporter";
        public const string PluginVersion = "0.1.1";

        private static VanillaAICExportRuntime runtime;
        private static bool libraryLoadedHandled;

        private void Awake()
        {
            if (runtime != null)
                return;

            runtime = new VanillaAICExportRuntime(Logger);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded; waiting for CrusaderDE.dll.");
        }

        private void OnDestroy()
        {
            // This plugin intentionally stays rooted after BepInEx destroys its manager GameObject.
            Shared.DebugLogHelper.LogInfo(Logger, "Plugin component destroyed during startup; export state remains process-wide.");
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (libraryLoadedHandled)
                return;

            try
            {
                if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(
                        Logger,
                        PluginName,
                        requireCurrentVersion: true))
                {
                    return;
                }

                runtime.Export();
                libraryLoadedHandled = true;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Vanilla AIC export failed: {ex}");
            }
        }
    }
}
