using System;
using BepInEx;
using SHCDESE.API.LowLevel;

namespace MultiplayerRestartTest
{
    [BepInPlugin(Guid, "Multiplayer Restart Test", "0.1.3")]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.62.1")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class RestartPlugin : BaseUnityPlugin
    {
        public const string Guid = "MultiplayerRestartTest_Serp";
        internal static RestartRuntime Runtime;
        private void Awake()
        {
            // Install the worker checkpoint before Director starts its process-lived thread.
            // Neither these hooks nor the plain static runtime are removed by startup cleanup.
            Runtime = new RestartRuntime(Logger);
            try { StopBarrier.InstallEarly(Logger); }
            catch (Exception error) { Runtime.Report("STOP_BARRIER_UNAVAILABLE " + error); }
            CrusaderLibrary.Instance.LibraryLoaded += Loaded;
        }
        private static void Loaded(CrusaderLibraryLoadContext context)
        {
            try { Runtime.Initialize(); }
            catch (Exception error) { Runtime.Report("INITIALIZATION_FAILED " + error); }
        }
    }
}
