using System;
using BepInEx;
using SHCDESE.API.LowLevel;

namespace MultiplayerSaveLoadTest
{
    [BepInPlugin(Guid, "Multiplayer Save Load Test", "0.2.6")]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.71.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class SaveLoadPlugin : BaseUnityPlugin
    {
        // Keep the existing isolated testmod identity and DLL path for replacement installs.
        public const string Guid = "MultiplayerRestartTest_Serp";
        internal static SaveLoadRuntime Runtime;
        private void Awake()
        {
            Runtime = new SaveLoadRuntime(Logger);
            try { LoadBarrier.InstallEarly(Logger); }
            catch (Exception error) { Runtime.Report("STOP_UNAVAILABLE " + error); }
            CrusaderLibrary.Instance.LibraryLoaded += Loaded;
        }
        private static void Loaded(CrusaderLibraryLoadContext context)
        {
            try { Runtime.Initialize(); }
            catch (Exception error) { Runtime.Report("INITIALIZATION_FAILED " + error); }
        }
    }
}
