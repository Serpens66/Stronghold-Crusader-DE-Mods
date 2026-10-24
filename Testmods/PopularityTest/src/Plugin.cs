using APIShared.ModSettings;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace PopularityTest
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.14.1")]
    [BepInDependency("APIShared_Serp", "0.15.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "PopularityTest_Serp", Name = "Popularity Test", Version = "0.2.1";
        private static ManualLogSource log;
        private static PopularitySettings settings;
        private static PopularityRuntime runtime;
        private static bool registered;
        private void Awake()
        {
            if (registered) return;
            registered = true; log = Logger; settings = new PopularitySettings();
            Shared.DebugLogHelper.InitializeDiagnostics(Config, log);
            try
            {
                LobbyModSettingsPresetRegistration.Register(this, log, Name, settings, "ScriptExtenderUI/PopularitySettings.xaml");
                // The library event and runtime subscriptions survive startup component destruction.
                CrusaderLibrary.Instance.LibraryLoaded += LibraryLoaded;
            }
            catch (Exception ex) { Failed(ex); }
        }
        private static void LibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try { var candidate = new PopularityRuntime(log, settings); candidate.Register(); runtime = candidate; }
            catch (Exception ex) { Failed(ex); }
        }
        private static void Failed(Exception ex) => Shared.DebugLogHelper.LogError(log, "POPULARITY_INIT_FAILED: " + ex);
    }
}
