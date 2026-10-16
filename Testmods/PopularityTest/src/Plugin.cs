using APIShared.ModSettings;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace PopularityTest
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.14.1")]
    [BepInDependency("APIShared_Serp", "0.9.2")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "PopularityTest_Serp", Name = "Popularity Test", Version = "0.1.0";
        private static ManualLogSource log;
        private static PopularitySettings settings;
        private static PopularityRuntime runtime;
        private static bool registered;
        private void Awake()
        {
            if (registered) return;
            registered = true; log = Logger; settings = new PopularitySettings();
            try
            {
                LobbyModSettingsPresetRegistration.Register(this, log, Name, settings, "ScriptExtenderUI/PopularitySettings.xaml");
                // The library event and runtime subscriptions survive startup component destruction.
                CrusaderLibrary.Instance.LibraryLoaded += LibraryLoaded;
                log.LogInfo("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] POPULARITY_BOOT version=" + Version);
            }
            catch (Exception ex) { Failed(ex); }
        }
        private static void LibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try { runtime = new PopularityRuntime(log, settings); runtime.Register(); }
            catch (Exception ex) { Failed(ex); }
        }
        private static void Failed(Exception ex) => log.LogError("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] POPULARITY_INIT_FAILED: " + ex);
    }
}
