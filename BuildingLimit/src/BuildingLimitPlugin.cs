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
        public const string PluginVersion = "1.0.1";

        private BuildingLimitRuntime runtime;
        private bool runtimeDisposed;

        public BuildingLimitLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");

            Settings = new BuildingLimitLobbyViewModel();
            runtime = new BuildingLimitRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogDebug(Logger, "BuildingLimitPlugin OnDestroy called; keeping runtime active until application quit.");
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
        }

        private void OnApplicationQuit()
        {
            Shared.DebugLogHelper.LogDebug(Logger, "BuildingLimitPlugin OnApplicationQuit called; disposing runtime.");
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

                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; BuildingLimit UI registered.");
                runtime.InitializeAfterLibraryLoaded();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Error while initializing BuildingLimit after library load: {ex}");
            }
        }
    }
}
