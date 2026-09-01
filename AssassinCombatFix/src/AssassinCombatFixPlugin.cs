using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace AssassinCombatFix
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(BugfixesAndQoLGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class AssassinCombatFixPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string BugfixesAndQoLGuid = "BugfixesAndQoL_Serp";
        private const string PluginGuid = "AssassinCombatFix_Serp";
        private const string PluginName = "Assassin Combat Fix";
        private const string PluginVersion = "0.1.0";

        // The BepInEx component is destroyed during startup. Static ownership keeps the
        // native hook alive for the complete process.
        private static ManualLogSource persistentLog;
        private static BugfixesAndQoL.BugfixesAndQoLViewModel settings;
        private static AssassinCombatResumeRuntime runtime;
        private static bool librarySubscriptionInstalled;

        private void Awake()
        {
            persistentLog = Logger;
            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded; the fix follows the BugfixesAndQoL Assassin-pathfinding setting.");

            if (!Chainloader.PluginInfos.TryGetValue(BugfixesAndQoLGuid, out var dependencyInfo) ||
                !(dependencyInfo.Instance is BugfixesAndQoL.BugfixesAndQoLPlugin dependencyPlugin) ||
                dependencyPlugin.Settings == null)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"{PluginName} could not obtain the required BugfixesAndQoL settings; Vanilla behavior remains active.");
                return;
            }

            settings = dependencyPlugin.Settings;

            if (librarySubscriptionInstalled)
                return;

            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            librarySubscriptionInstalled = true;
        }

        private static void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
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
                    return;

                AssassinCombatResumeRuntime installed = new AssassinCombatResumeRuntime(persistentLog, settings);
                installed.InitializeNative(libraryHandle, memory, referenceHashMatches);
                runtime = installed;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"{PluginName} could not install its native hook; Vanilla behavior remains active: {ex}");
            }
        }
    }
}
