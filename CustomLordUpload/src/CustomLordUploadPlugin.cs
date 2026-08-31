using BepInEx;
using System;

namespace CustomLordUpload
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class CustomLordUploadPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "CustomLordUpload_Serp";
        public const string PluginName = "Custom Lord Upload";
        public const string PluginVersion = "1.0.0";

        // The BepInEx component is destroyed during startup, so the process-lifetime detour stays rooted here.
        private static CustomLordUploadHook? uploadHook;

        private void Awake()
        {
            if (uploadHook != null)
                return;

            try
            {
                uploadHook = new CustomLordUploadHook(Logger);
                Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded; Workshop upload hook installed.");
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(Logger, $"{PluginName} initialization failed; Vanilla uploads remain unchanged: {exception}");
            }
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogDebug(Logger, "Plugin component destroyed during startup; keeping the Custom Lord upload hook rooted.");
        }
    }
}
