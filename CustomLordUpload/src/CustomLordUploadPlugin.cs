using BepInEx;
using System;

namespace CustomLordUpload
{
    [BepInDependency(ScriptExtenderGuid, "2.2.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class CustomLordUploadPlugin : BaseUnityPlugin
    {
        // COMPATIBILITY: Recheck the hard dependency GUID after Script Extender packaging changes.
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "CustomLordUpload_Serp";
        public const string PluginName = "Custom Lord Upload";
        public const string PluginVersion = "1.0.1";

        // COMPATIBILITY: The BepInEx component is destroyed during startup in the current loader;
        // recheck lifecycle behavior after BepInEx/Script Extender updates. Process-lifetime services stay rooted here.
        private static CustomLordUploadHook? uploadHook;
        private static CustomLordUploadOptionsController? uploadOptions;
        private static CustomLordPopupPatchVerifier? popupPatchVerifier;

        private void Awake()
        {
            if (uploadHook != null)
                return;

            try
            {
                popupPatchVerifier = new CustomLordPopupPatchVerifier(Logger);
                uploadOptions = new CustomLordUploadOptionsController(Logger);
                uploadHook = new CustomLordUploadHook(Logger, uploadOptions);
                uploadOptions.InitializeEditorHook();
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
