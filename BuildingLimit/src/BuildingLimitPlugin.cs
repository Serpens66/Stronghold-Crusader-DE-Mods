using APIShared.ModSettings;
using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Threading;

namespace BuildingLimit
{
    [BepInDependency(ScriptExtenderGuid, "2.14.0")]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("APIShared_Serp", "0.9.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class BuildingLimitPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        internal const string CrusaderDeTweakerGuid = "CrusaderDETweaker";

        public const string PluginGuid = "BuildingLimit_Serp";
        public const string PluginName = "Building Limit";
        public const string PluginVersion = "1.0.32";

        private static BuildingLimitRuntime runtime;
        private int libraryInitializationStarted;

        public BuildingLimitLobbyViewModel Settings { get; private set; }

        private void Awake()
        {

            Settings = new BuildingLimitLobbyViewModel();
            runtime = new BuildingLimitRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            // A late subscription can race with the regular event raise; initialize only once.
            if (Interlocked.Exchange(ref libraryInitializationStarted, 1) != 0)
                return;

            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;

            TryInitializeStage("native version diagnostics", () => Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName));
            TryInitializeStage("localized names", Settings.RefreshLocalizedNames);
            try
            {
                Shared.DirectLaunchSettingsNotice.Configure(Settings, PluginGuid);
                APIShared.ModSettings.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    "BuildingLimit_Serp",
                    Settings,
                    "ScriptExtenderUI/BuildingLimitSettings.xaml");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BuildingLimit/src/BuildingLimitPlugin.cs:58", $"BuildingLimit settings registration failed; gameplay runtime stopped fail-closed: {ex}");
                return;
            }

            TryInitializeStage("notification binding", () => GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BuildingLimitNotificationOverlay",
                    runtime.BuildingLimitNotification));
            try
            {
                runtime.InitializeAfterLibraryLoaded();
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; BuildingLimit runtime initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BuildingLimit/src/BuildingLimitPlugin.cs:79", $"Error while initializing BuildingLimit after library load: {ex}");
            }
        }

        private void TryInitializeStage(string stageName, Action initialize)
        {
            try { initialize(); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "BuildingLimit/src/BuildingLimitPlugin.cs:88", $"BuildingLimit {stageName} failed; independent stages continue: {ex}");
            }
        }
    }
}
