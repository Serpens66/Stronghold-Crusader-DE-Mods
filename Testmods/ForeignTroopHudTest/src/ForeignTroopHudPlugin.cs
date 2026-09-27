using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;

namespace ForeignTroopHudTest
{
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("ForeignTroopHudTest_Serp", "Foreign Troop HUD Test", "0.1.0")]
    public sealed class ForeignTroopHudPlugin : BaseUnityPlugin
    {
        private static ManualLogSource log;

        private void Awake()
        {
            log = Logger;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            ForeignTroopHudRuntime.Initialize(log);
        }
    }
}
