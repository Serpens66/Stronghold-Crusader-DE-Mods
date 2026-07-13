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
        public const string PluginVersion = "1.0.15";

        private SomeSettingsRuntime runtime;
        private bool runtimeDisposed;

        public SomeSettingsViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");

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

                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "SomeSettingsKnightDismountButtonHost",
                    runtime.KnightDismountButton);

                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "SomeSettingsQuarryPileRelocationButtonHost",
                    runtime.QuarryPileRelocationButton);

                runtime.InstallKnightMountNativeFunctions(libraryHandle, memory);
                runtime.ApplySettings();
                runtime.InstallAIEconomyProtectionHook(libraryHandle, memory);
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; SomeSettings UI registered.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Error while initializing SomeSettings after library load: {ex}");
            }
        }
    }
}
