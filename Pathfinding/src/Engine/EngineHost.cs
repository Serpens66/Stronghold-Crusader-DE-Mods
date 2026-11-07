// Feature: Hosts the movement engine provider (former standalone MoatMove plugin).
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace Pathfinding.Engine
{
    internal static class EngineHost
    {
        internal const string PluginGuid = PathfindingPlugin.PluginGuid;
        private static ManualLogSource persistentLog;
        private static FriendlyMoatTraversalProvider runtime;
        private static EngineOptions options;
        private static IDisposable missionInitializationSubscription, mapStartSubscription, mapUnloadSubscription;
        private static volatile bool missionOptionsFrozen;
        private static bool mapActive, mapTickObserved;
        private static int mapSequence;

        internal static void Install(CrusaderLibraryLoadContext context, ManualLogSource log, PathfindingViewModel settings, string pluginDirectory)
        {
            if (runtime != null) return;
            persistentLog = log;
            options = EngineOptions.Capture(settings, pluginDirectory);
            if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(log, "Pathfinding engine", requireCurrentVersion: true)) return;
            // Registration roots the candidate before native publication. Failure disables its
            // policy; published command hooks remain owned by the command runtime.
            runtime = new FriendlyMoatTraversalProvider(log, options);
            runtime.Install(context);
            ReportEngineState();
            Shared.DebugLogHelper.LogInfo(log, "Pathfinding engine published; anchorSingleMoves=" + options.AnchorSingleMoves +
                ", avoidFire=" + options.AvoidFire + ", avoidPlague=" + options.AvoidPlague + ".");
            try
            {
                // Freeze the host-synchronized values before the map loads; a start path without
                // that phase freezes them when the session starts. Each mission applies them once.
                missionInitializationSubscription = Shared.MissionEvents.Initialization.Subscribe(notification =>
                {
                    if (notification.Phase == APIShared.MissionInitializationPhase.BeforeLoad) FreezeMissionOptions(settings, pluginDirectory);
                });
                mapStartSubscription = Shared.GameplaySessionLifecycle.SubscribeStarted(log, _ =>
                {
                    if (!missionOptionsFrozen) FreezeMissionOptions(settings, pluginDirectory);
                    ObserveMapStart();
                });
                mapUnloadSubscription = Shared.MissionEvents.Ended.Subscribe(_ => { missionOptionsFrozen = false; ObserveMapUnload(); });
                GameTimeManagerAPI.Instance.OnTick += ObserveMapTick;
            }
            catch (Exception ex)
            {
                // Diagnostic registration must not tear down the published movement runtime.
                Shared.DebugLogHelper.LogError(log, "Pathfinding lifecycle diagnostics unavailable: " + ex);
            }
        }

        private static void FreezeMissionOptions(PathfindingViewModel settings, string pluginDirectory)
        {
            missionOptionsFrozen = true;
            options = EngineOptions.Capture(settings, pluginDirectory);
            runtime.ApplyMissionOptions(options);
            ReportEngineState();
            Shared.DebugLogHelper.LogInfo(persistentLog, "Pathfinding mission options frozen; core=" + options.EnablePathfindingCore +
                ", friendlyMoats=" + options.EnableFriendlyMoatTraversal + ", singleMoves=" + options.AnchorSingleMoves +
                ", avoidFire=" + options.AvoidFire + ", avoidPlague=" + options.AvoidPlague + ".");
        }

        // Tells rule mods whether their rules apply in this mission (diagnostics and option texts only).
        private static void ReportEngineState()
        {
            if (!options.EnablePathfindingCore) { APIShared.Pathfinding.RouteEngineInfo.Clear(PluginGuid); return; }
            var features = APIShared.Pathfinding.RouteEngineFeatures.TileRules | APIShared.Pathfinding.RouteEngineFeatures.RouteCosts;
            if (options.AnchorSingleMoves) features |= APIShared.Pathfinding.RouteEngineFeatures.ReplanPolicies;
            if (!APIShared.Pathfinding.RouteEngineInfo.TryReport(PluginGuid, features, out string reason))
                Shared.DebugLogHelper.LogWarning(persistentLog, "Pathfinding engine status not reported: " + reason);
        }

        private static void ObserveMapStart()
        {
            mapSequence++; mapActive = true; mapTickObserved = false;
            Shared.DebugLogHelper.LogInfo(persistentLog, $"Pathfinding map-start sequence={mapSequence}; runtime active.");
        }

        private static void ObserveMapTick(int tick)
        {
            if (!mapActive || mapTickObserved) return;
            mapTickObserved = true;
            Shared.DebugLogHelper.LogInfo(persistentLog, $"Pathfinding post-startup-tick sequence={mapSequence} tick={tick}; runtime rooted.");
        }

        private static void ObserveMapUnload()
        {
            Shared.DebugLogHelper.LogInfo(persistentLog, $"Pathfinding map-unload sequence={mapSequence} tickObserved={mapTickObserved}; process hooks retained.");
            mapActive = false;
        }
    }
}
