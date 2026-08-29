using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
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
        // diagnostic hooks alive for the complete process.
        private static ManualLogSource persistentLog;
        private static AssassinCombatResumeRuntime runtime;
        private static IDisposable mapStartSubscription;
        private static bool librarySubscriptionInstalled;

        private void Awake()
        {
            persistentLog = Logger;
            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded; Assassin combat-resume diagnostics are always active.");

            if (mapStartSubscription == null)
            {
                mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => runtime?.BeginMap());
            }

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

                AssassinCombatResumeRuntime installed = new AssassinCombatResumeRuntime(persistentLog);
                installed.InitializeNative(libraryHandle, memory, referenceHashMatches);
                runtime = installed;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"{PluginName} could not install its diagnostic hooks; Vanilla behavior remains active: {ex}");
            }
        }
    }
}
