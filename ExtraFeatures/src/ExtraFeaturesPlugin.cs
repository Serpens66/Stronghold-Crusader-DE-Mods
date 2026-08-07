// Feature: Plugin bootstrap for the Extra Features mod.
using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace ExtraFeatures
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(BugfixesAndQoLGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ExtraFeaturesPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string BugfixesAndQoLGuid = "BugfixesAndQoL_Serp";

        public const string PluginGuid = "ExtraFeatures_Serp";
        public const string PluginName = "Extra Features";
        public const string PluginVersion = "1.0.0";

        private ExtraFeaturesRuntime runtime;
        private bool runtimeDisposed;

        public ExtraFeaturesViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");
            Settings = new ExtraFeaturesViewModel();
            runtime = new ExtraFeaturesRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        // The BepInEx manager destroys this component during startup, so only process quit may clean up.
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
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/ExtraFeaturesSettings.xaml");

                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "ExtraFeaturesKnightDismountButtonHost",
                    runtime.KnightDismountButton);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "ExtraFeaturesQuarryPileRelocationButtonHost",
                    runtime.QuarryPileRelocationButton);

                runtime.InitializeNative(
                    libraryHandle,
                    memory,
                    Shared.DebugLogHelper.IsCurrentNativeLibraryVersion());
                runtime.ApplySettings();
                runtime.InstallAIEconomyProtectionHook(libraryHandle, memory);
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; Extra Features initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Error while initializing Extra Features after library load: {ex}");
            }
        }
    }
}
