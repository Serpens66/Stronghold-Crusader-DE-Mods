using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace MoveMoatTest
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(BugfixesAndQoLGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class MoveMoatTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string BugfixesAndQoLGuid = "BugfixesAndQoL_Serp";
        private const string PluginGuid = "MoveMoatTest_Serp";
        private const string PluginName = "Move Moat Test";
        private const string PluginVersion = "1.0.0";

        // BepInEx destroys this component during startup. Static ownership keeps the
        // native detours and their delegates rooted for the complete process.
        private static ManualLogSource persistentLog;
        private static MoveMoatPathTest feature;
        private static bool librarySubscriptionInstalled;

        private void Awake()
        {
            persistentLog = Logger;
            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded; friendly/allied completed-moat movement and enemy-owner diagnostics are enabled.");

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

                feature = new MoveMoatPathTest(
                    persistentLog,
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()),
                    referenceHashMatches);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"{PluginName} could not install the central moat-path test; Vanilla behavior remains active: {ex}");
            }
        }
    }
}
