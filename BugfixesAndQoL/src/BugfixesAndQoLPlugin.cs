// Feature: Plugin bootstrap for the Bugfixes and QoL mod.
using BepInEx;
using BepInEx.Bootstrap;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace BugfixesAndQoL
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(LegacySomeSettingsGuid, BepInDependency.DependencyFlags.SoftDependency)]
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
        private const string LegacySomeSettingsGuid = "SomeSettings_Serp";

        public const string PluginGuid = "BugfixesAndQoL_Serp";
        public const string PluginName = "Bugfixes and QoL";
        public const string PluginVersion = "1.0.4";

        private BugfixesAndQoLRuntime runtime;
        private bool runtimeDisposed;

        public BugfixesAndQoLViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");
            bool legacySomeSettingsLoaded = Chainloader.PluginInfos.ContainsKey(LegacySomeSettingsGuid);
            if (legacySomeSettingsLoaded)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"The obsolete mod '{LegacySomeSettingsGuid}' is loaded together with {PluginName}. " +
                    "Uninstall it to avoid duplicate or conflicting features.");
            }

            // Pass the startup result into the view model so the warning occupies no UI space otherwise.
            Settings = new BugfixesAndQoLViewModel(legacySomeSettingsLoaded);
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
            // Keep UI registration independent so one native feature cannot hide the whole mod.
            try
            {
                runtime.InitializeNative(
                    libraryHandle,
                    memory,
                    Shared.DebugLogHelper.IsCurrentNativeLibraryVersion());
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Bugfixes and QoL native runtime initialization failed; unaffected features may continue: {ex}");
            }

            try
            {
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/BugfixesAndQoLSettings.xaml");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Bugfixes and QoL settings UI registration failed: {ex}");
            }

            try
            {
                runtime.ApplySettings();
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; Bugfixes and QoL initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Bugfixes and QoL settings reconciliation failed; already initialized features remain active: {ex}");
            }
        }
    }
}
