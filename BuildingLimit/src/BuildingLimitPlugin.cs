using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Threading;

namespace BuildingLimit
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class BuildingLimitPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "BuildingLimit_Serp";
        public const string PluginName = "Building Limit";
        public const string PluginVersion = "1.0.10";

        private BuildingLimitRuntime runtime;
        private int libraryInitializationStarted;

        public BuildingLimitLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");

            Settings = new BuildingLimitLobbyViewModel();
            runtime = new BuildingLimitRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            // A late subscription can race with the regular event raise; initialize only once.
            if (Interlocked.Exchange(ref libraryInitializationStarted, 1) != 0)
                return;

            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;

            try
            {
                Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName);
                Settings.RefreshLocalizedNames();
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
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
