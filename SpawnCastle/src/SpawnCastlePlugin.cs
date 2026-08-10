using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace SpawnCastle
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SpawnCastlePlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "SpawnCastle_Serp";
        public const string PluginName = "Spawn Castle";
        public const string PluginVersion = "0.4.3";

        // The BepInEx component is destroyed during startup, so runtime state remains static.
        private static SpawnCastleRuntime runtime;
        private static BlueprintRuntimeController blueprintRuntime;
        private static CastleDropDownHeightController castleDropDownHeightController;
        private static bool libraryLoadedHandled;

        public SpawnCastleSettingsViewModel Settings { get; private set; }

        private void Awake()
        {
            if (runtime != null)
                return;

            Settings = new SpawnCastleSettingsViewModel(Logger, Info.Location);
            runtime = new SpawnCastleRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            Shared.DebugLogHelper.LogInfo(
                Logger,
                $"{PluginName} {PluginVersion} loaded; AIV choices={Settings.AvailableFileCount}.");
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogInfo(
                Logger,
                "Plugin component destroyed during startup; keeping SpawnCastle lifecycle subscriptions rooted.");
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (libraryLoadedHandled)
                return;

            try
            {
                bool currentNativeLayout =
                    Shared.DebugLogHelper.ReportNativeLibraryVersion(
                        Logger,
                        PluginName,
                        requireCurrentVersion: true);
                Shared.DebugLogHelper.LogInfo(
                    Logger,
                    $"Registering local SpawnCastle presets: " +
                    $"storage=LobbyModSettings/{PluginGuid}.msgpack, enabled={Settings.EnableMod}, " +
                    $"mode={Settings.Mode}, selection='{Settings.SelectedCastle}'.");

                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/SpawnCastleSettings.xaml");
                castleDropDownHeightController =
                    CastleDropDownHeightController.Attach(Logger, Settings);
                blueprintRuntime =
                    BlueprintRuntimeController.Create(Logger, Settings);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "SpawnCastleBlueprintHud",
                    blueprintRuntime.Hud);
                Shared.DebugLogHelper.LogInfo(
                    Logger,
                    $"SpawnCastle settings registration completed: " +
                    $"enabled={Settings.EnableMod}, mode={Settings.Mode}, " +
                    $"selection='{Settings.SelectedCastle}', " +
                    $"hotkey={Settings.HotkeyDisplayText}.");

                try
                {
                    // Blueprint mode is managed; only the native Spawn path depends on
                    // the currently audited AIV structure layout.
                    if (currentNativeLayout)
                        runtime.Install(libraryHandle, memory);
                }
                catch (Exception ex)
                {
                    // Blueprint mode is managed and remains useful when a future
                    // game version invalidates the native Spawn signatures.
                    Shared.DebugLogHelper.LogError(
                        Logger,
                        $"Native Spawn mode initialization failed; " +
                        $"local Blueprint mode remains available: {ex}");
                }

                libraryLoadedHandled = true;
                Shared.DebugLogHelper.LogInfo(
                    Logger,
                    "Crusader library initialization completed; local Blueprint mode is registered and native Spawn mode was initialized when supported.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"SpawnCastle initialization failed: {ex}");
            }
        }
    }
}
