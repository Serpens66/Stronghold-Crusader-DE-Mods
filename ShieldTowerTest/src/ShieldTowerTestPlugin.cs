using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace ShieldTowerTest
{
    [BepInDependency(ScriptExtenderGuid, "2.2.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ShieldTowerTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "ShieldTowerTest_Serp";
        private const string PluginName = "Shield Tower Test";
        private const string PluginVersion = "0.1.0";

        // SHCDE destroys the BepInEx component during normal startup. Static ownership
        // intentionally keeps this experimental native hook alive for the process.
        private static ManualLogSource persistentLog;
        private static PortableShieldClimbOverride feature;
        private static bool librarySubscriptionInstalled;

        private void Awake()
        {
            persistentLog = Logger;
            Shared.DebugLogHelper.LogWarning(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded. This unfinished research mod may crash the game.");

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

                PortableShieldClimbOverride installed = new PortableShieldClimbOverride(
                    persistentLog,
                    context,
                    referenceHashMatches);
                installed.SetEnabled(true);
                feature = installed;

                Shared.DebugLogHelper.LogWarning(
                    persistentLog,
                    $"{PluginName} experimental portable-shield override is active.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"{PluginName} could not install the experimental override; Vanilla remains active: {ex}");
            }
        }
    }
}
