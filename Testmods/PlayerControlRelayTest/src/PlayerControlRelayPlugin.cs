using System;
using BepInEx;
using SHCDESE.API.LowLevel;

namespace PlayerControlRelayTest
{
    [BepInPlugin(Guid, "Player Control Relay Test", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.27.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class PlayerControlRelayPlugin : BaseUnityPlugin
    {
        public const string Guid = "PlayerControlRelayTest_Serp";
        private static RelayRuntime runtime;
        private void Awake()
        {
            Shared.DebugLogHelper.PersistentHashDirectory = System.IO.Path.Combine(Paths.BepInExRootPath, "cache", Guid);
            // A static plain runtime and process-lived publishers survive startup cleanup.
            runtime = new RelayRuntime(Logger);
            CrusaderLibrary.Instance.LibraryLoaded += Loaded;
        }
        private static void Loaded(CrusaderLibraryLoadContext context)
        {
            try { runtime.Initialize(context); }
            catch (Exception error) { runtime.Report("Initialization failed: " + error); }
        }
    }
}
