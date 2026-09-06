using BepInEx;
using SHCDESE.API.LowLevel;
using System;
using System.Threading;

namespace TooltipTest
{
    [BepInDependency(ScriptExtenderGuid, "2.2.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class TooltipTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        public const string PluginGuid = "TooltipTest_Serp";
        public const string PluginName = "Tooltip Test";
        public const string PluginVersion = "0.1.0";

        // The registered settings view remains rooted by the Script Extender after SHCDE
        // destroys the early BepInEx component during its normal startup cleanup.
        private static TooltipTestViewModel persistentSettings;
        private static int registrationStarted;

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded.");
            if (persistentSettings == null)
                persistentSettings = new TooltipTestViewModel();

            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (Interlocked.Exchange(ref registrationStarted, 1) != 0)
                return;

            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
            try
            {
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    PluginGuid,
                    persistentSettings,
                    "ScriptExtenderUI/TooltipTestSettings.xaml");
                Shared.DebugLogHelper.LogInfo(Logger, "Tooltip test settings UI registered; no gameplay runtime is present.");
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Tooltip test settings registration failed: {exception}");
            }
        }
    }
}
