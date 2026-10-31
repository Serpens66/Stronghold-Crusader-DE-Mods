using AIBuildDiagnoseTest;
using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using System;
namespace CanarariWoodTest
{
    [BepInDependency("000shcdese", "2.11.0")]
    [BepInDependency("APIShared_Serp", "0.4.6")]
    [BepInDependency("BugfixesAndQoL_Serp", "1.0.181")]
    [BepInDependency("AIBuildDiagnoseTest_Serp", "0.1.2")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, "CanarariWoodTest", "0.1.0")]
    public sealed class CanarariWoodPlugin : BaseUnityPlugin
    {
        public const string Guid = "CanarariWoodTest_Serp";
        private static ManualLogSource log;
        private static bool registered, placement, nearby, farms, swap;
        private static bool experimentsReady;
        private static FixesFarmFilterOverride comparison;
        private static CanarariWoodRuntime runtime;
        private static CanariPassiveDiagnostics passive;
        private static CanariNativeCapture capture;
        private static IDisposable sessionSubscription, spawnSubscription, buildSubscription;
        private void Awake()
        {
            log = Logger;
            placement = Config.Bind("PlacementProbe", "Enabled", false, "One-time placement probe on test_canari_nowoodcutters_probe.sav only. Never save the result.").Value;
            nearby = Config.Bind("NearbyWoodTest", "Enabled", false, "Calibrated temporary candidate overlay on the named Canari save copy, at most twelve calls.").Value;
            farms = Config.Bind("FarmContractProbe", "Enabled", false, "Place four farms and test sites on rat_farm_site_contract_probe.sav only. Never save the result.").Value;
            swap = Config.Bind("CanariFarmSwap", "Enabled", false, "Replace the orchard on three byte-verified test_canari_farm_swap_*.sav copies only. Never save the result.").Value;
            bool suppress = Config.Bind("FixesFarmFilterOverride", "Enabled", false,
                "Before native load, suppress the external Fixes farmland filter for a Vanilla comparison. Process-wide until game exit; restart to change. The configuration file is preserved.").Value;
            if (registered) return;
            try { comparison = FixesFarmFilterOverride.Begin(log, suppress); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_FIXES_FILTER: startup check failed; probes disabled: " + ex); }
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            registered = true;
            Shared.DebugLogHelper.LogInfo(log, "CanarariWoodTest loaded; placement=" + placement + ", nearby=" + nearby +
                ", farms=" + farms + ", swap=" + swap + ", suppressFixesFilter=" + suppress + "; startup-rooted=true.");
        }
        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            bool ready = false;
            try { ready = comparison != null && comparison.Complete(context.ModuleHandle); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_FIXES_FILTER: compatibility check failed: " + ex); }
            if (!ready && (placement || nearby || farms || swap))
                Shared.DebugLogHelper.LogWarning(log, "AI_BUILD_SPECIAL_PROBES_DISABLED: Vanilla comparison/native contract unavailable; no active experiment armed.");
            try
            {
                passive = new CanariPassiveDiagnostics(log, BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("fixes"),
                    unchecked((ulong)context.ModuleHandle.ToInt64()),
                    () => BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("AICoarsePathComponentFixTest_Serp"));
                runtime = new CanarariWoodRuntime(log, unchecked((ulong)context.ModuleHandle.ToInt64()),
                    ready && placement, ready && nearby, ready && farms, ready && swap, passive.Log);
                capture = new CanariNativeCapture(context.ModuleHandle.ToInt64(), NativeDiagnosticContracts.ComputeInstalledHash());
                if (!AiBuildExperimentBridge.TryRegister(Guid, DeliverRecord, out string error) ||
                    !AiBuildExperimentBridge.TryRegisterDetails(Guid, capture, passive.OnWall, out error))
                { Shared.DebugLogHelper.LogError(log, "AI_BUILD_SPECIAL_PROBES_UNAVAILABLE: " + error); runtime = null; return; }
                experimentsReady = AiBuildDiagnostic.TryRegisterWoodBuildGate(Guid, runtime.ShouldDeferWoodBuild, out error) &&
                    AiBuildDiagnostic.TryRegisterNearbyWoodOverlay(Guid, runtime.BeginNearbyWoodOverlay, out error);
                if (!experimentsReady)
                    Shared.DebugLogHelper.LogError(log, "AI_BUILD_SPECIAL_PROBES_UNAVAILABLE: " + error + "; passive observation remains active.");
                sessionSubscription = Shared.GameplaySessionLifecycle.SubscribeStarted(log, OnSessionStarted, OnSessionEnded);
                spawnSubscription = BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnExperimentSpawn);
                buildSubscription = BuildingR3EventHooks.OnBuildStructure.Observable.Subscribe(OnExperimentBuild);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                Shared.DebugLogHelper.LogInfo(log, "AI_BUILD_SPECIAL_READY: publisher=GameTimeManagerAPI.OnTick; nativeHooksOwnedBy=AIBuildDiagnoseTest.");
            }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_SPECIAL_INITIALIZATION_FAILED: " + ex); }
        }
        private static bool firstTick;
        private static void DeliverRecord(AiBuildDiagnosticRecord record)
        {
            try { passive?.OnNativeRecord(record); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_PASSIVE_CALLBACK_FAILED: " + ex); }
            try { if (experimentsReady) runtime?.OnNativeRecord(record); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_EXPERIMENT_CALLBACK_FAILED: " + ex); }
        }
        private static void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            try { passive?.OnSessionStarted(session); }
            finally { if (experimentsReady) runtime?.OnSessionStarted(session); }
        }
        private static void OnSessionEnded()
        {
            try { passive?.OnSessionEnded(); }
            finally { runtime?.OnSessionEnded(); }
        }
        private static void OnExperimentSpawn(BuildingSpawnEventArgs args)
        { if (experimentsReady) runtime?.OnBuildingSpawn(args); }
        private static void OnExperimentBuild(BuildStructureEventArgs args)
        { if (experimentsReady) runtime?.OnBuildStructure(args); }
        private static void OnTick(int tick)
        {
            if (!firstTick) { firstTick = true; Shared.DebugLogHelper.LogInfo(log, "AI_BUILD_SPECIAL_POST_STARTUP_TICK: " + tick); }
            try { passive?.OnTick(tick); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_PASSIVE_TICK_FAILED: " + ex); }
            try { if (experimentsReady) runtime?.OnTick(tick); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_BUILD_SPECIAL_TICK_FAILED: " + ex); }
        }
    }
}
