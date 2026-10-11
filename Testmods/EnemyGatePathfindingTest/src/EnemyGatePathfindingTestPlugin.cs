using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Units;
using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace EnemyGatePathfindingTest
{
    [BepInDependency(ScriptExtenderGuid, "2.14.0")]
    // Load after the hook owner when it exists, so PluginInfos can suppress
    // our overlapping observational route hooks while keeping the PCL hook active.
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("APIShared_Serp", "0.3.6")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class EnemyGatePathfindingTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "EnemyGatePathfindingTest_Serp";
        private const string PluginName = "Enemy Gate Pathfinding Test";
        private const string PluginVersion = "0.1.6";

        // The BepInEx component is destroyed during startup. Static ownership keeps the
        // native hook and event subscriptions alive for the complete process.
        private static ManualLogSource persistentLog;
        private static bool detailedDiagnostics;
        private static EnemyGatePathfindingRuntime runtime;
        private static IDisposable mapStartSubscription;
        private static IDisposable mapUnloadSubscription;
        private static IDisposable targetOrderSubscription;
        private static IDisposable tribeMoveSubscription;
        private static IDisposable unitMoveSubscription;
        private static IDisposable buildingCaptureSubscription;
        private static bool librarySubscriptionInstalled;
        private static bool beforeRenderInstalled;
        private static bool gameTickInstalled;
        private static int lastDiagnosticFrame = -1;

        private void Awake()
        {
            persistentLog = Logger;
            detailedDiagnostics = Config.Bind("Diagnostics", "DetailedDiagnostics", false,
                "Enable detailed gate and temporary Raid/Assassin diagnostics. Restart the game after changing this local option.").Value;
            LogScriptExtenderIdentity();
            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"{PluginName} {PluginVersion}: DetailedDiagnostics={detailedDiagnostics}, " +
                $"hookOwner={(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("BugfixesAndQoL_Serp") ? "BugfixesAndQoL" : "standalone")}; local option requires restart.");

            // UPDATE REVIEW (Script Extender): revalidate map event phases and lifetime;
            // the BaseUnityPlugin component itself is intentionally not the runtime owner.
            if (mapStartSubscription == null)
            {
                mapStartSubscription = Shared.GameplaySessionLifecycle.SubscribeStarted(
                    persistentLog,
                    context => runtime?.BeginMap(context.IsEditor));
            }
            if (mapUnloadSubscription == null)
            {
                mapUnloadSubscription = Shared.MissionEvents.Ended
                    .Subscribe(_ => runtime?.EndMap("MissionEnd"));
            }
            if (detailedDiagnostics && targetOrderSubscription == null)
            {
                // The public Script Extender event brackets Vanilla 0x11E960. This
                // observer never mutates or suppresses the order.
                targetOrderSubscription = TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable
                    .Subscribe(ObserveTargetOrder);
            }
            if (detailedDiagnostics && tribeMoveSubscription == null)
                tribeMoveSubscription = TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                    .Subscribe(ObserveTribeMove);
            if (detailedDiagnostics && unitMoveSubscription == null)
                unitMoveSubscription = UnitR3EventHooks.OnUnitMoveHere.Observable
                    .Subscribe(ObserveUnitMove);
            if (buildingCaptureSubscription == null)
                buildingCaptureSubscription = BuildingR3EventHooks.OnBuildingCapture.Observable
                    .Subscribe(args => runtime?.ObserveBuildingCapture(args));
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
                    requireCurrentVersion: true, logSuccess: detailedDiagnostics);
                if (!referenceHashMatches)
                    return;

                var installed = new EnemyGatePathfindingRuntime(persistentLog, detailedDiagnostics);
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

        private static void ObserveTargetOrder(TribeIssueOrderWithTargetEventArgs args) =>
            runtime?.ObserveTargetOrder(args);

        private static void ObserveTribeMove(TribeIssueOrderMoveHereEventArgs args) =>
            runtime?.ObserveTribeMove(args);

        private static void ObserveUnitMove(UnitMoveHereEventArgs args) =>
            runtime?.ObserveUnitMove(args);

        private static void LogScriptExtenderIdentity()
        {
            try
            {
                // Official builds do not embed the Git commit in their informational
                // version; the exact audited identity is informational, never a load requirement.
                Assembly assembly = typeof(CrusaderLibrary).Assembly;
                string informational = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion ?? "unknown";
                string location = assembly.Location;
                string fileVersion = string.IsNullOrEmpty(location)
                    ? "unknown"
                    : FileVersionInfo.GetVersionInfo(location).FileVersion;
                bool auditedVersion = assembly.GetName().Version == new Version(EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderVersion + ".0");
                if (detailedDiagnostics) Shared.DebugLogHelper.LogInfo(
                    persistentLog,
                    $"Script Extender identity: manifestVersionRange=true, auditIdentityOnly=true, " +
                    $"auditedVersion={EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderVersion}, " +
                    $"auditedTag={EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderTag}, " +
                    $"auditedCommit={EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderCommit}, " +
                    $"auditedTree={EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderTree}, " +
                    $"auditedAssemblySha256={EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderSha256}, " +
                    $"assembly={assembly.FullName}, fileVersion={fileVersion}, informationalVersion={informational}, " +
                    $"auditedVersionMatch={auditedVersion}, " +
                    $"redBirdAudited={EnemyGatePathfindingNativeDefinition.AuditedRedBirdVersion}.");
                if (!auditedVersion)
                {
                    Shared.DebugLogHelper.LogWarning(
                        persistentLog,
                        $"Script Extender differs from audited version " +
                        $"{EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderVersion}. " +
                        "Review native and API contracts before accepting test results.");
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
