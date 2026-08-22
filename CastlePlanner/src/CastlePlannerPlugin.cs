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
        public const string PluginVersion = "0.6.5";

        // The BepInEx component is destroyed during startup, so runtime state remains static.
        private static CastlePlannerRuntime runtime;
        private static BlueprintRuntimeController blueprintRuntime;
        private static CastleDropDownHeightController castleDropDownHeightController;
        private static CastlePlanner.AIVPlacement.AivPlacementRuntime aivPlacementRuntime;
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
                $"{PluginName} {PluginVersion} loaded; the AIVJSON catalog will be cached when mod settings are opened.");
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
                    $"blueprintSelection='{Settings.SelectedCastle}', personalSpawnSelection='{Settings.SpawnSelectedCastle}'.");

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
                aivPlacementRuntime = new CastlePlanner.AIVPlacement.AivPlacementRuntime(
                    Logger,
                    () => Settings.EnableAivPlacementLobby);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "CastlePlannerAivSelectionListHost",
                    aivPlacementRuntime.SelectionList);
                aivPlacementRuntime.Install();
                Shared.DebugLogHelper.LogInfo(
                    Logger,
                    $"CastlePlanner settings registration completed: " +
                    $"clientEnabled={Settings.EnableClientFeatures}, hostEnabled={Settings.EnableMod}, " +
                    $"blueprints={Settings.Blueprints}, spawnCastle={Settings.SpawnCastle}, " +
                    $"blueprintSelection='{Settings.SelectedCastle}', personalSpawnSelection='{Settings.SpawnSelectedCastle}', " +
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
