using System;
using BepInEx;
using BepInEx.Logging;

namespace CrashDiagnostics
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.53.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed partial class CrashDiagnosticsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "CrashDiagnostics_Serp";
        public const string PluginName = "Crash Diagnostics";
        public const string PluginVersion = "0.1.1";

        private void Awake()
        {
            Shared.DevDiag.Attach(Logger);
            bool active = false;
            InitializeDiagnostics(Logger, System.IO.Path.GetDirectoryName(Info.Location), ref active);
            Logger.LogInfo("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " +
                PluginName + " " + PluginVersion + (active ? " diagnostic runtime initialized." :
                " inactive (normal build or initialization failed)."));
        }

        // Compiled away in a normal build, including the entire active runtime and subscriptions.
        partial void InitializeDiagnostics(ManualLogSource logger, string pluginDirectory, ref bool active);
    }
}
