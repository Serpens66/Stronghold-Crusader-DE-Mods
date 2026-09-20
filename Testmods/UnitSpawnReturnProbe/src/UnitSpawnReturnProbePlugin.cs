using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace UnitSpawnReturnProbe
{
    [BepInDependency(ScriptExtenderGuid, "2.8.0")]
    [BepInDependency("APIShared_Serp", "0.3.6")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class UnitSpawnReturnProbePlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "UnitSpawnReturnProbe_Serp";
        public const string PluginName = "Unit Spawn Return Probe";
        public const string PluginVersion = "0.1.0";

        private static UnitSpawnReturnProbeRuntime persistentRuntime;
        private static bool libraryLoadedSubscriptionInstalled;

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded.");

            if (persistentRuntime == null)
                persistentRuntime = new UnitSpawnReturnProbeRuntime(Logger);

            if (libraryLoadedSubscriptionInstalled)
                return;

            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            libraryLoadedSubscriptionInstalled = true;
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            try
            {
                if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(
                        Logger,
                        PluginName,
                        requireCurrentVersion: true))
                {
                    return;
                }

                persistentRuntime.Apply();
                Shared.DebugLogHelper.LogInfo(Logger, "Crusader library loaded; Unit Spawn Return Probe initialized.");
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Unit Spawn Return Probe initialization failed: {exception}");
            }
        }
    }
}
