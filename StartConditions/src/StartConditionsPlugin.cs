using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Threading;

namespace StartConditions
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class StartConditionsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "StartConditions_Serp";
        public const string PluginName = "Start Conditions";
        public const string PluginVersion = "1.0.14";

        private StartConditionsRuntime runtime;
        private int libraryInitializationStarted;

        public StartConditionsLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");

            Settings = new StartConditionsLobbyViewModel();
            runtime = new StartConditionsRuntime(Logger, Settings);
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
                Settings.RefreshLocalizedNames(message => Shared.DebugLogHelper.LogDebug(Logger, message));
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    "StartConditions_Serp",
                    Settings,
                    "ScriptExtenderUI/StartConditionsSettings.xaml");

                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; StartConditions UI registered.");
                runtime.InitializeAfterLibraryLoaded();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Error while initializing StartConditions after library load: {ex}");
            }
        }
    }
}
