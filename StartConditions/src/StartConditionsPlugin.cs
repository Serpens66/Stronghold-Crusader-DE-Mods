using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Threading;

namespace StartConditions
{
    [BepInDependency(ScriptExtenderGuid, "2.3.0")]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("APIShared_Serp", "0.4.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class StartConditionsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "StartConditions_Serp";
        public const string PluginName = "Start Conditions";
        public const string PluginVersion = "1.0.28";

        private StartConditionsRuntime runtime;
        private int libraryInitializationStarted;

        public StartConditionsLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            SerpLocalization.SetRoutineLoggingEnabled(false);
            Settings = new StartConditionsLobbyViewModel();
            runtime = new StartConditionsRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            // A late subscription can race with the regular event raise; initialize only once.
            if (Interlocked.Exchange(ref libraryInitializationStarted, 1) != 0)
                return;

            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;

            bool currentNativeVersion = false;
            TryInitializeStage(
                "native version diagnostics",
                () => currentNativeVersion = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    Logger,
                    PluginName,
                    logSuccess: false));
            TryInitializeStage("localized names", () => Settings.RefreshLocalizedNames());
            try
            {
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    "StartConditions_Serp",
                    Settings,
                    "ScriptExtenderUI/StartConditionsSettings.xaml",
                    logRoutineActivity: false);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"StartConditions settings registration failed; gameplay runtime stopped fail-closed: {ex}");
                return;
            }

            try
            {
                runtime.InitializeAfterLibraryLoaded(context, currentNativeVersion);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Error while initializing StartConditions after library load: {ex}");
            }
        }

        private void TryInitializeStage(string stageName, Action initialize)
        {
            try { initialize(); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"StartConditions {stageName} failed; independent stages continue: {ex}");
            }
        }
    }
}
