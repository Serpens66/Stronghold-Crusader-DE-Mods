using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;

namespace ForeignTroopHudTest
{
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("APIShared_Serp", "0.4.3")]
    [BepInPlugin(Guid, "Foreign Troop HUD Test", "0.1.0")]
    public sealed class ForeignTroopHudPlugin : BaseUnityPlugin
    {
        internal const string Guid = "ForeignTroopHudTest_Serp";
        private static ManualLogSource log;

        private void Awake()
        {
            log = Logger;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            try { ForeignTroopHudRuntime.Initialize(log); }
            catch (System.Exception error) { log.LogError("FOREIGN_TROOP_HUD_INITIALIZATION_FAILED: " + error); }
        }
    }
}
