using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace EnemyGatePathfindingTest
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    // Load after the optional test mod when it exists, so PluginInfos can suppress
    // our overlapping observational route hooks while keeping the PCL hook active.
    [BepInDependency("MoveMoatTest_Serp", BepInDependency.DependencyFlags.SoftDependency)]
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
        private static IDisposable moveHereSubscription;
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
            // UPDATE REVIEW (Script Extender): 1.42.0 owns the native detour at
            // CrusaderDE.dll RVA 0x196280. Its synchronous Pre/Post event deliberately
            // observes the issued command without installing a competing native hook;
            // cursor/PCL validation happens earlier and is correlated by timestamp.
            if (moveHereSubscription == null)
            {
                moveHereSubscription = UnitR3EventHooks.OnUnitMoveHere.Observable
                    .Subscribe(args => runtime?.ObserveMoveHere(args));
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
                if (!gameTickInstalled)
                {
                    // UPDATE REVIEW (Script Extender 1.42.0): OnTick is used only to
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
