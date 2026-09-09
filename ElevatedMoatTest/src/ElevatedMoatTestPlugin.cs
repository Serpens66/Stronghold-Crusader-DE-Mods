using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace ElevatedMoatTest
{
    [BepInDependency(ScriptExtenderGuid, "2.3.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ElevatedMoatTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "ElevatedMoatTest_Serp";
        private const string PluginName = "Elevated Moat Test";
        private const string PluginVersion = "0.1.0";

        // SHCDE destroys early BepInEx components during startup. Static ownership keeps
        // both the logger and the native hook alive for the remainder of the process.
        private static ManualLogSource persistentLog;
        private static ElevatedMoatOverride feature;
        private static bool librarySubscriptionInstalled;

        private void Awake()
        {
            persistentLog = Logger;
            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded. This gameplay-affecting test applies to every player.");

            if (librarySubscriptionInstalled)
                return;

            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            librarySubscriptionInstalled = true;
        }

        private static void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (feature != null)
                return;

            try
            {
                bool referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    persistentLog,
                    PluginName,
                    requireCurrentVersion: true);
                if (!referenceHashMatches)
                    return;

                feature = new ElevatedMoatOverride(persistentLog, context, referenceHashMatches);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"{PluginName} could not install its hook; Vanilla remains unchanged: {exception}");
            }
        }
    }
}
