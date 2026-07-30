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
        public const string PluginVersion = "0.1.6";

        // The BepInEx component is destroyed during startup, so runtime state remains static.
        private static SpawnCastleRuntime runtime;
        private static bool libraryLoadedHandled;

        public SpawnCastleSettingsViewModel Settings { get; private set; }

        private void Awake()
        {
            if (runtime != null)
                return;

            Settings = new SpawnCastleSettingsViewModel(Logger);
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
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                    this,
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/SpawnCastleSettings.xaml");
                Settings.RewriteInvalidPersistedSelectionIfNeeded();

                runtime.Install();
                libraryLoadedHandled = true;
                Shared.DebugLogHelper.LogInfo(
                    Logger,
                    "Crusader library loaded; SpawnCastle settings and map lifecycle hooks registered. " +
                    "Selection storage=LobbyModSettings/SpawnCastle_Serp.msgpack.");
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
