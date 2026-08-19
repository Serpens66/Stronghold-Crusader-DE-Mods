using BepInEx;
using SHCDESE.API.LowLevel;
using SHCDESE.API;
using System;
using System.IO;

namespace CustomCustomTrail
{
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class CustomCustomTrailPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "CustomCustomTrail_Serp";
        public const string PluginName = "Custom Custom Trail";
        public const string PluginVersion = "1.3.12";

        private static CustomCustomTrailRuntime runtime;

        public CustomCustomTrailSettingsViewModel Settings { get; private set; }

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
                Settings = new CustomCustomTrailSettingsViewModel();
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                    this,
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/CustomCustomTrailSettings.xaml");
                string customTrailsRoot = ConfigSettings.GetUserCustomTrailsPath();
                runtime = new CustomCustomTrailRuntime(Logger, customTrailsRoot, Settings);
                runtime.Initialize();
                Settings.EnableModChanged += runtime.SetEnabled;
            }
            catch (Exception ex)
            {
                try
                {
                    runtime?.Dispose();
                }
                catch (Exception cleanupException)
                {
                    Shared.DebugLogHelper.LogWarning(Logger, "CustomCustomTrail partial initialization cleanup failed: " + cleanupException);
                }
                runtime = null;
                Shared.DebugLogHelper.LogError(Logger, "CustomCustomTrail initialization failed: " + ex);
            }
        }

        private void OnDestroy()
        {
            // The BepInEx manager object is destroyed during startup; runtime hooks must survive it.
            Shared.DebugLogHelper.LogDebug(Logger, "CustomCustomTrail OnDestroy called; keeping process-lifetime runtime active.");
        }

        private void OnApplicationQuit()
        {
            runtime?.Dispose();
            runtime = null;
        }
    }
}
