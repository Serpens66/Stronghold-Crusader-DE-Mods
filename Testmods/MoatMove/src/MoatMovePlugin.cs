using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using System;
using System.Reflection;

[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0")]

namespace MoatMove
{
    [BepInPlugin(PluginGuid, "MoatMove", PluginVersion)]
    [BepInDependency("000shcdese", "2.5.0")]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("EnemyGatePathfindingTest_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class MoatMovePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "MoatMove_Serp";
        public const string PluginVersion = "0.1.0";
        private static ManualLogSource persistentLog;
        private static FriendlyMoatMovementRuntime runtime;
        private static readonly MoatMoveOptions options = new MoatMoveOptions();
        private static IDisposable mapStartSubscription;
        private static IDisposable mapUnloadSubscription;
        private static bool initialized;
        private static bool mapActive;
        private static bool mapTickObserved;
        private static int mapSequence;

        private void Awake()
        {
            if (initialized) return;
            initialized = true;
            persistentLog = Logger;
            if (ReportConflict()) return;
            Shared.DebugLogHelper.LogInfo(persistentLog,
                "MoatMove 0.1.0 loaded; precise always active, improvedFill=false, ladderAttackFix=false, formationEnhancements=false; awaiting native library.");
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private static bool ReportConflict()
        {
            string conflict = MoatMoveConflictPolicy.FindConflict(Chainloader.PluginInfos.Keys);
            if (conflict == null) return false;
            Shared.DebugLogHelper.LogError(persistentLog,
                $"MoatMove disabled before hook installation: conflicting plugin {conflict} is loaded. Test MoatMove alone or with APIShared; remove the conflicting plugin from this test configuration.");
            return true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null || ReportConflict()) return;
            try
            {
                bool referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    persistentLog, "MoatMove", requireCurrentVersion: true);
                if (!referenceHashMatches) return;
                // Constructor rolls back failed initialization. A successfully published
                // runtime is process-owned and is never disposed by a plugin/map event.
                runtime = new FriendlyMoatMovementRuntime(persistentLog, options, context, referenceHashMatches);
                Shared.DebugLogHelper.LogInfo(persistentLog,
                    "MoatMove runtime published; comparison=precise; APIShared is optional; detailed diagnostics disabled.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(persistentLog, $"MoatMove initialization failed: {ex}");
                return;
            }

            try
            {
                mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => ObserveMapStart());
                mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(_ => ObserveMapUnload());
                GameTimeManagerAPI.Instance.OnTick += ObserveMapTick;
            }
            catch (Exception ex)
            {
                // Diagnostic registration must not tear down the published movement runtime.
                Shared.DebugLogHelper.LogError(persistentLog, $"MoatMove lifecycle diagnostics unavailable: {ex}");
            }
        }

        private static void ObserveMapStart()
        {
            mapSequence++;
            mapActive = true;
            mapTickObserved = false;
            Shared.DebugLogHelper.LogInfo(persistentLog, $"MoatMove map-start sequence={mapSequence}; precise active.");
        }

        private static void ObserveMapTick(int tick)
        {
            if (!mapActive || mapTickObserved) return;
            mapTickObserved = true;
            Shared.DebugLogHelper.LogInfo(persistentLog,
                $"MoatMove post-startup-tick sequence={mapSequence} tick={tick}; runtime rooted.");
        }

        private static void ObserveMapUnload()
        {
            Shared.DebugLogHelper.LogInfo(persistentLog,
                $"MoatMove map-unload sequence={mapSequence} tickObserved={mapTickObserved}; process hooks retained.");
            mapActive = false;
        }
    }
}
