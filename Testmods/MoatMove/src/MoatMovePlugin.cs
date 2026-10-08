using APIShared.UnitCommands;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using System;
using System.Reflection;

[assembly: AssemblyVersion("0.1.4.0")]
[assembly: AssemblyFileVersion("0.1.4.0")]
[assembly: AssemblyInformationalVersion("0.1.4")]

namespace MoatMove
{
    [BepInDependency("APIShared_Serp", "0.4.10")]
    [BepInPlugin(PluginGuid, "MoatMove", PluginVersion)]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("BugfixesAndQoL_Serp", "1.0.175")]
    [BepInDependency("EnemyGatePathfindingTest_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class MoatMovePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "MoatMove_Serp";
        public const string PluginVersion = "0.1.4";
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
            string mode = Config.Bind("Movement", "Mode", "precise",
                new ConfigDescription(
                    "precise: weighted friendly/allied moat routes. fast: moat only when no ground alternative exists, shared group calculations, no extra moat cost. FastNative: Fast rules with a private native shared field. Restart the game after changing this setting. Use the same mode on all multiplayer peers.")).Value;
            options = new MoatMoveOptions(mode);
            Shared.DebugLogHelper.LogInfo(persistentLog,
                $"MoatMove {PluginVersion} loaded; mode={options.ModeName}, commandPolicy=BugfixesAndQoL, hookOwner=APIShared; awaiting native library.");
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
                // disables its policy; published hooks remain owned by APIShared.
                runtime = new FriendlyMoatTraversalProvider(persistentLog, options);
                runtime.Install(context);
                Shared.DebugLogHelper.LogInfo(persistentLog,
                    $"MoatMove runtime published; mode={options.ModeName}; shared command hooks owned by APIShared; detailed diagnostics disabled.");
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
