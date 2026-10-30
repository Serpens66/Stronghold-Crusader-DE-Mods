extern alias BugfixesRuntime;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using System;
using System.Reflection;

[assembly: AssemblyVersion("0.1.5.0")]
[assembly: AssemblyFileVersion("0.1.5.0")]
[assembly: AssemblyInformationalVersion("0.1.5")]

namespace MoatMove
{
    [BepInDependency("APIShared_Serp", "0.4.10")]
    [BepInPlugin(PluginGuid, "MoatMove", PluginVersion)]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("BugfixesAndQoL_Serp", "1.0.181")]
    [BepInDependency("EnemyGatePathfindingTest_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class MoatMovePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "MoatMove_Serp";
        public const string PluginVersion = "0.1.5";
        private static ManualLogSource persistentLog;
        private static FriendlyMoatTraversalProvider runtime;
        private static MoatMoveOptions options;
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
            // DEVDIAG builds only: Shared.DevDiag measuring points write through this log.
            Shared.DevDiag.Attach(Logger);
            string mode = Config.Bind("Movement", "Mode", "precise",
                new ConfigDescription(
                    "precise: weighted friendly/allied moat routes. fast: moat only when no ground alternative exists, shared group calculations, no extra moat cost. FastNative: Fast rules with a private native shared field. anchor (experimental): Fast's deferred move commands with native group fields; moves cross the moat when the weighted route is cheaper. Restart the game after changing this setting. Use the same mode on all multiplayer peers.")).Value;
            bool singleMoves = Config.Bind("Movement", "AnchorSingleMoves", false,
                new ConfigDescription("anchor mode only (experimental): single-unit ground moves outside group commands also use the native core (exact cheapest routes, route tile rules) instead of Vanilla's per-unit search. Routes can differ from Vanilla's. Restart the game after changing this setting. Use the same value on all multiplayer peers.")).Value;
            bool avoidFire = Config.Bind("Movement", "AvoidFire", false,
                new ConfigDescription("anchor mode only (experimental): routes avoid burning pitch. With AnchorSingleMoves, units that are already walking when fire starts on their route re-plan 3 tiles before it (a detour may be 50 % or 10 tiles longer) or wait up to about 10 s. Restart the game after changing this setting. Use the same value on all multiplayer peers.")).Value;
#if SERP_DEVDIAG
            int ruleProbeTicks = Config.Bind("Diagnostics", "RuleProbeTicks", 0,
                new ConfigDescription("DEVDIAG only: simulation ticks after a map start at which a forbidden test band (x 399..401, every row except 380..420) is set for all players; 0 = off.")).Value;
            int moatCostProbeTicks = Config.Bind("Diagnostics", "MoatCostProbeTicks", 0,
                new ConfigDescription("DEVDIAG only: simulation ticks after a map start from which a RouteSearchEvents observer makes moat edges 50 times as expensive as ground edges; 0 = off.")).Value;
            int topologyProbeTicks = Config.Bind("Diagnostics", "TopologyProbeTicks", 0,
                new ConfigDescription("DEVDIAG only: simulation ticks after a map start at which every player's cursor topology is rebuilt once with the table-based and the per-tile build (both timed); 0 = off.")).Value;
#else
            int ruleProbeTicks = 0, moatCostProbeTicks = 0, topologyProbeTicks = 0;
#endif
            options = new MoatMoveOptions(mode) { PluginDirectory = System.IO.Path.GetDirectoryName(Info.Location), AnchorSingleMoves = singleMoves, AvoidFire = avoidFire,
                RuleProbeTicks = ruleProbeTicks, MoatCostProbeTicks = moatCostProbeTicks, TopologyProbeTicks = topologyProbeTicks };
            Shared.DebugLogHelper.LogInfo(persistentLog,
                $"MoatMove {PluginVersion} loaded; mode={options.ModeName}, commandPolicy=BugfixesAndQoL, hookOwner=BugfixesAndQoL; awaiting native library.");
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try
            {
                bool referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    persistentLog, "MoatMove", requireCurrentVersion: true);
                if (!referenceHashMatches) return;
                // Registration roots the candidate before native publication. Failure
                // disables its policy; published command hooks remain owned by BugfixesAndQoL.
                runtime = new FriendlyMoatTraversalProvider(persistentLog, options);
                runtime.Install(context);
                Shared.DebugLogHelper.LogInfo(persistentLog,
                    $"MoatMove runtime published; mode={options.ModeName}; shared command hooks owned by BugfixesAndQoL; detailed diagnostics disabled.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(persistentLog, $"MoatMove initialization failed: {ex}");
                return;
            }

            try
            {
                mapStartSubscription = Shared.GameplaySessionLifecycle.SubscribeStarted(
                    persistentLog,
                    _ => ObserveMapStart());
                mapUnloadSubscription = Shared.MissionEvents.Ended
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
            Shared.DebugLogHelper.LogInfo(persistentLog, $"MoatMove map-start sequence={mapSequence}; mode={options.ModeName} active.");
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
