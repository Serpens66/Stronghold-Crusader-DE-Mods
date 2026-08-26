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
        public const string PluginVersion = "1.0.15";

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

            TryInitializeStage("native version diagnostics", () => Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName));
            TryInitializeStage("localized names", Settings.RefreshLocalizedNames);
            try
            {
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    "BuildingLimit_Serp",
                    Settings,
                    "ScriptExtenderUI/BuildingLimitSettings.xaml");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"BuildingLimit settings registration failed; gameplay runtime stopped fail-closed: {ex}");
                return;
            }

            TryInitializeStage("notification binding", () => GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BuildingLimitNotificationOverlay",
                    runtime.BuildingLimitNotification));
            TryInitializeStage("detailed tooltip binding", () => GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BuildingLimitTooltipHost",
                    runtime.BuildingLimitTooltip));
            TryInitializeStage("compact tooltip binding", () => GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BuildingLimitTooltipHostCompact",
                    runtime.BuildingLimitTooltip));

            try
            {
                runtime.InitializeAfterLibraryLoaded();
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; BuildingLimit runtime initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Error while initializing BuildingLimit after library load: {ex}");
            }
        }

        private void TryInitializeStage(string stageName, Action initialize)
        {
            try { initialize(); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"BuildingLimit {stageName} failed; independent stages continue: {ex}");
            }
        }
    }
}
