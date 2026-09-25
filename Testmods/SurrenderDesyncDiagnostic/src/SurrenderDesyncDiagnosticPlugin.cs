using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace SurrenderDesyncDiagnostic
{
    [BepInDependency("000shcdese", "2.10.0")]
    [BepInDependency("APIShared_Serp", "0.4.0")]
    [BepInDependency("BugfixesAndQoL_Serp", "1.0.164")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SurrenderDesyncDiagnosticPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "SurrenderDesyncDiagnostic_Serp";
        public const string PluginName = "Surrender Desync Diagnostic";
        public const string PluginVersion = "0.1.0";

        private static ManualLogSource persistentLog;
        private static SurrenderDesyncDiagnosticRuntime runtime;
        private static bool subscribed;

        private void Awake()
        {
            persistentLog = Logger;
            if (subscribed)
                return;
            // The library publisher and static runtime outlive SHCDE's startup cleanup.
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            subscribed = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null)
                return;
            try
            {
                runtime = new SurrenderDesyncDiagnosticRuntime(persistentLog);
                runtime.Initialize();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(persistentLog,
                    "Surrender diagnostic initialization failed closed: " + ex);
            }
        }
    }
}
