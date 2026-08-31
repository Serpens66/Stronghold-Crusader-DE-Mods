using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using System;
using System.Diagnostics;
using System.Reflection;

namespace EnemyGatePathfindingTest
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class EnemyGatePathfindingTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "EnemyGatePathfindingTest_Serp";
        private const string PluginName = "Enemy Gate Pathfinding Test";
        private const string PluginVersion = "0.1.0";

        // The BepInEx component is destroyed during startup. Static ownership keeps the
        // native hook and event subscriptions alive for the complete process.
        private static ManualLogSource persistentLog;
        private static EnemyGatePathfindingRuntime runtime;
        private static IDisposable mapStartSubscription;
        private static IDisposable mapUnloadSubscription;
        private static bool librarySubscriptionInstalled;

        private void Awake()
        {
            persistentLog = Logger;
            LogScriptExtenderIdentity();
            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded; no settings or dependencies beyond Script Extender are used.");

            // UPDATE REVIEW (Script Extender): revalidate map event phases and lifetime;
            // the BaseUnityPlugin component itself is intentionally not the runtime owner.
            if (mapStartSubscription == null)
            {
                mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => runtime?.BeginMap());
            }
            if (mapUnloadSubscription == null)
            {
                mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => runtime?.EndMap());
            }
            if (librarySubscriptionInstalled)
                return;

            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            librarySubscriptionInstalled = true;
        }

        // UPDATE REVIEW (Script Extender): revalidate LibraryLoaded timing, mapped-memory
        // span semantics and the native module handle before installing either hook.
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

                var installed = new EnemyGatePathfindingRuntime(persistentLog);
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

        private static void LogScriptExtenderIdentity()
        {
            try
            {
                // UPDATE REVIEW (Script Extender): this identity check deliberately warns
                // after any rebuild/update so every API/layout marker is re-audited.
                Assembly assembly = typeof(CrusaderLibrary).Assembly;
                string informational = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion ?? "unknown";
                string location = assembly.Location;
                string fileVersion = string.IsNullOrEmpty(location)
                    ? "unknown"
                    : FileVersionInfo.GetVersionInfo(location).FileVersion;
                bool auditedCommit = informational.IndexOf(
                    EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderCommit,
                    StringComparison.OrdinalIgnoreCase) >= 0;
                Shared.DebugLogHelper.LogInfo(
                    persistentLog,
                    $"Script Extender identity: auditedVersion={EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderVersion}, " +
                    $"auditedCommit={EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderCommit}, " +
                    $"assembly={assembly.FullName}, fileVersion={fileVersion}, informationalVersion={informational}, " +
                    $"auditedCommitMatch={auditedCommit}.");
                if (!auditedCommit)
                {
                    Shared.DebugLogHelper.LogWarning(
                        persistentLog,
                        "Script Extender differs from the audited 1.42.0 commit. Review every UPDATE REVIEW marker before accepting test results.");
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    persistentLog,
                    $"Script Extender identity could not be reported; review every UPDATE REVIEW marker: {ex.Message}");
            }
        }
    }
}
