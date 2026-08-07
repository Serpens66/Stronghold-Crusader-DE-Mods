// Feature: Plugin bootstrap for the Bugfixes and QoL mod.
using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace BugfixesAndQoL
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInIncompatibility(LegacyTroopMovementFixGuid)]
    [BepInIncompatibility(TroopMovementFix2Guid)]
    [BepInIncompatibility(TroopMovementFix3Guid)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class BugfixesAndQoLPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string LegacyTroopMovementFixGuid = "TroopMovementFix_Serp";
        private const string TroopMovementFix2Guid = "TroopMovementFix2_Serp";
        private const string TroopMovementFix3Guid = "TroopMovementFix3_Serp";

        public const string PluginGuid = "BugfixesAndQoL_Serp";
        public const string PluginName = "Bugfixes and QoL";
        public const string PluginVersion = "1.0.0";

        private BugfixesAndQoLRuntime runtime;
        private bool runtimeDisposed;

        public BugfixesAndQoLViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");
            Settings = new BugfixesAndQoLViewModel();
            runtime = new BugfixesAndQoLRuntime(Logger, Settings);
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
                    "ScriptExtenderUI/BugfixesAndQoLSettings.xaml");

                runtime.InitializeNative(
                    libraryHandle,
                    memory,
                    Shared.DebugLogHelper.IsCurrentNativeLibraryVersion());
                runtime.ApplySettings();
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; Bugfixes and QoL initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Error while initializing Bugfixes and QoL after library load: {ex}");
            }
        }
    }
}
