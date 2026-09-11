using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace EnemyGatePathfindingTest
{
    [BepInDependency(ScriptExtenderGuid, "2.5.0")]
    // Load after the hook owner when it exists, so PluginInfos can suppress
    // our overlapping observational route hooks while keeping the PCL hook active.
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class EnemyGatePathfindingTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "EnemyGatePathfindingTest_Serp";
        private const string PluginName = "Enemy Gate Pathfinding Test";
        private const string PluginVersion = "0.1.3";

        // The BepInEx component is destroyed during startup. Static ownership keeps the
        // native hook and event subscriptions alive for the complete process.
        private static ManualLogSource persistentLog;
        private static EnemyGatePathfindingRuntime runtime;
        private static IDisposable mapStartSubscription;
        private static IDisposable mapUnloadSubscription;
        private static bool librarySubscriptionInstalled;
        private static bool beforeRenderInstalled;
        private static bool gameTickInstalled;
        private static int lastDiagnosticFrame = -1;

        private void Awake()
        {
            persistentLog = Logger;
            LogScriptExtenderIdentity();
            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded; no settings and no hard dependency beyond Script Extender are used.");

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
            if (!beforeRenderInstalled)
            {
                // UPDATE REVIEW (Unity/Script Extender): this proven persistent static
                // callback drains diagnostics only after native simulation work returned.
                Application.onBeforeRender += ProcessDeferredDiagnostics;
                beforeRenderInstalled = true;
            }
            if (librarySubscriptionInstalled)
                return;

            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            librarySubscriptionInstalled = true;
        }

        private static void ProcessDeferredDiagnostics()
        {
            int frame = Time.frameCount;
            if (lastDiagnosticFrame == frame)
                return;
            lastDiagnosticFrame = frame;
            runtime?.ProcessDeferredDiagnostics();
        }

        // UPDATE REVIEW (Script Extender): revalidate LibraryLoaded timing, mapped-memory
        // span semantics and the native module handle before installing either hook.
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
                    return;

                var installed = new EnemyGatePathfindingRuntime(persistentLog);
                installed.InitializeNative(context, referenceHashMatches);
                runtime = installed;
                if (!gameTickInstalled)
                {
                    // Script Extender OnTick is used only to
                    // invalidate accepted gate state; rebuilding remains deferred.
                    GameTimeManagerAPI.Instance.OnTick += ProcessGameTick;
                    gameTickInstalled = true;
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"{PluginName} could not install its native hook; Vanilla behavior remains active: {ex}");
            }
        }

        private static void ProcessGameTick(int tick) => runtime?.OnGameTick();

        private static void LogScriptExtenderIdentity()
        {
            try
            {
                // Official builds do not embed the Git commit in their informational
                // version, so the assembly version is the enforceable runtime identity.
                Assembly assembly = typeof(CrusaderLibrary).Assembly;
                string informational = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion ?? "unknown";
                string location = assembly.Location;
                string fileVersion = string.IsNullOrEmpty(location)
                    ? "unknown"
                    : FileVersionInfo.GetVersionInfo(location).FileVersion;
                bool auditedVersion = assembly.GetName().Version == new Version(2, 5, 0, 0);
                Shared.DebugLogHelper.LogInfo(
                    persistentLog,
                    $"Script Extender identity: manifestVersionRange=true, " +
                    $"auditedCommit={EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderCommit}, " +
                    $"assembly={assembly.FullName}, fileVersion={fileVersion}, informationalVersion={informational}, " +
                    $"auditedVersionMatch={auditedVersion}.");
                if (!auditedVersion)
                {
                    Shared.DebugLogHelper.LogWarning(
                        persistentLog,
                        "Script Extender differs from audited version 2.5.0. Review native and API contracts before accepting test results.");
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
