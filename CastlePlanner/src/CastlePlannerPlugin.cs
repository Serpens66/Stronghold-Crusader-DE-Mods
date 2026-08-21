using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace CastlePlanner
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class CastlePlannerPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "CastlePlanner_Serp";
        public const string PluginName = "CastlePlanner";
        public const string PluginVersion = "0.4.9";

        // The BepInEx component is destroyed during startup, so runtime state remains static.
        private static CastlePlannerRuntime runtime;
        private static BlueprintRuntimeController blueprintRuntime;
        private static CastleDropDownHeightController castleDropDownHeightController;
        private static bool libraryLoadedHandled;

        public CastlePlannerSettingsViewModel Settings { get; private set; }

        private void Awake()
        {
            if (runtime != null)
                return;

            Settings = new CastlePlannerSettingsViewModel(Logger, Info.Location);
            runtime = new CastlePlannerRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            Shared.DebugLogHelper.LogInfo(
                Logger,
                $"{PluginName} {PluginVersion} loaded; AIV choices={Settings.AvailableFileCount}.");
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogInfo(
                Logger,
                "Plugin component destroyed during startup; keeping CastlePlanner lifecycle subscriptions rooted.");
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
                    $"Registering local CastlePlanner presets: " +
                    $"storage=LobbyModSettings/{PluginGuid}.msgpack, " +
                    $"clientEnabled={Settings.EnableClientFeatures}, hostEnabled={Settings.EnableMod}, " +
                    $"blueprints={Settings.Blueprints}, spawnCastle={Settings.SpawnCastle}, " +
                    $"blueprintSelection='{Settings.SelectedCastle}', hostSelection='{Settings.HostSelectedCastle}'.");

                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/CastlePlannerSettings.xaml");
                castleDropDownHeightController =
                    CastleDropDownHeightController.Attach(Logger, Settings);
                blueprintRuntime =
                    BlueprintRuntimeController.Create(Logger, Settings);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "CastlePlannerBlueprintHud",
                    blueprintRuntime.Hud);
                Shared.DebugLogHelper.LogInfo(
                    Logger,
                    $"CastlePlanner settings registration completed: " +
                    $"clientEnabled={Settings.EnableClientFeatures}, hostEnabled={Settings.EnableMod}, " +
                    $"blueprints={Settings.Blueprints}, spawnCastle={Settings.SpawnCastle}, " +
                    $"blueprintSelection='{Settings.SelectedCastle}', hostSelection='{Settings.HostSelectedCastle}', " +
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
                    $"CastlePlanner initialization failed: {ex}");
            }
        }
    }
}
