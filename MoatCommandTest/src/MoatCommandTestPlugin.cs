using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace MoatCommandTest
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class MoatCommandTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "MoatCommandTest_Serp";
        private const string PluginName = "Moat Command Test";
        private const string PluginVersion = "1.0.0";

        // The BepInEx component is destroyed during startup. Static ownership keeps the
        // native hooks alive for the complete process, matching the Script Extender lifecycle.
        private static ManualLogSource persistentLog;
        private static MoatDiggingReachabilityFix feature;
        private static bool librarySubscriptionInstalled;

        private void Awake()
        {
            persistentLog = Logger;
            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded; the extracted moat feature is always active.");

            if (librarySubscriptionInstalled)
                return;

            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            librarySubscriptionInstalled = true;
        }

        private static void OnCrusaderLibraryLoaded(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory)
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

                feature = new MoatDiggingReachabilityFix(
                    persistentLog,
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()),
                    referenceHashMatches);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"{PluginName} could not install the extracted feature; Vanilla behavior remains active: {ex}");
            }
        }
    }
}
