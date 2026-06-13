using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace BuildingLimit
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class BuildingLimitPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "BuildingLimit_Serp";
        public const string PluginName = "Building Limit";
        public const string PluginVersion = "0.1.0";

        private BuildingLimitRuntime runtime;
        private bool runtimeDisposed;

        public BuildingLimitLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            Logger.LogDebug($"{PluginName} {PluginVersion} loaded.");

            Settings = new BuildingLimitLobbyViewModel();
            runtime = new BuildingLimitRuntime(Logger, Settings);
            runtime.SubscribeHooks();
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnDestroy()
        {
            Logger.LogDebug("BuildingLimitPlugin OnDestroy called; keeping runtime active until application quit.");
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
        }

        private void OnApplicationQuit()
        {
            Logger.LogDebug("BuildingLimitPlugin OnApplicationQuit called; disposing runtime.");
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
                Settings.RefreshLocalizedNames();
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                    this,
                    "BuildingLimit_Serp",
                    Settings,
                    "ScriptExtenderUI/BuildingLimitSettings.xaml");
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BuildingLimitNotificationOverlay",
                    runtime.BuildingLimitNotification);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BuildingLimitTooltipHost",
                    runtime.BuildingLimitTooltip);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BuildingLimitTooltipHostCompact",
                    runtime.BuildingLimitTooltip);

                Logger.LogDebug("Crusader library loaded; BuildingLimit UI registered.");
                runtime.InitializeAfterLibraryLoaded();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error while initializing BuildingLimit after library load: {ex}");
            }
        }
    }
}
