using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace SomeSettings
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SomeSettingsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "SomeSettings_Serp";
        public const string PluginName = "Some Settings";
        public const string PluginVersion = "1.0.5";

        private SomeSettingsRuntime runtime;
        private bool runtimeDisposed;

        public SomeSettingsViewModel Settings { get; private set; }

        private void Awake()
        {
            Logger.LogDebug($"{PluginName} {PluginVersion} loaded.");

            Settings = new SomeSettingsViewModel();
            runtime = new SomeSettingsRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnDestroy()
        {
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
        }

        private void OnApplicationQuit()
        {
            DisposeRuntime();
        }

        private void DisposeRuntime()
        {
            if (runtimeDisposed)
                return;

            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
            runtime?.Dispose();
            runtimeDisposed = true;
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            try
            {
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                    this,
                    "SomeSettings_Serp",
                    Settings,
                    "ScriptExtenderUI/SomeSettingsSettings.xaml");

                runtime.ApplySettings();
                runtime.InstallAIEconomyProtectionHook(libraryHandle, memory);
                Logger.LogDebug("Crusader library loaded; SomeSettings UI registered.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error while initializing SomeSettings after library load: {ex}");
            }
        }
    }
}
