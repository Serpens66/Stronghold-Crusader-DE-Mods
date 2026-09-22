using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace WaterboyTargetReservationTest
{
    [BepInDependency(ScriptExtenderGuid, "2.7.2")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class WaterboyTargetReservationPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "WaterboyTargetReservationTest_Serp";
        private const string PluginName = "Waterboy Target Reservation Test";
        private const string PluginVersion = "0.1.0";

        private static ManualLogSource persistentLog;
        private static WaterboyTargetReservationRuntime runtime;
        private static bool librarySubscriptionInstalled;

        private void Awake()
        {
            persistentLog = Logger;
            Shared.DebugLogHelper.LogWarning(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded; testMod=true, gameplaySynchronized=true, settings=false.");

            if (librarySubscriptionInstalled)
                return;

            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            librarySubscriptionInstalled = true;
        }

        private static void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null)
                return;

            try
            {
                bool referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    persistentLog,
                    PluginName,
                    requireCurrentVersion: true);
                if (!referenceHashMatches)
                {
                    Shared.DebugLogHelper.LogWarning(
                        persistentLog,
                        $"{PluginName} remains inactive because the canonical CrusaderDE.dll hash did not match; Vanilla remains active.");
                    return;
                }

                runtime = new WaterboyTargetReservationRuntime(persistentLog, context, referenceHashMatches);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"{PluginName} could not install its target-selection detour; Vanilla remains active: {exception}");
            }
        }
    }
}
