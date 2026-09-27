using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;

namespace SpectatorPerspectiveTest
{
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("SpectatorPerspectiveTest_Serp", "Spectator Perspective Test", "0.1.0")]
    public sealed class SpectatorPerspectivePlugin : BaseUnityPlugin
    {
        private static ManualLogSource log;

        private void Awake()
        {
            log = Logger;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            SpectatorPerspectiveRuntime.Initialize(log);
        }
    }
}
