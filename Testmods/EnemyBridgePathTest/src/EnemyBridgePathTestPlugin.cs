using BepInEx;
using BepInEx.Configuration;
using R3;
using SHCDESE.API.LowLevel;
using System;

namespace EnemyBridgePathTest
{
    [BepInPlugin("EnemyBridgePathTest_Serp", "Enemy Bridge Path Test", "0.1.1")]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.49.0")]
    public sealed class EnemyBridgePathTestPlugin : BaseUnityPlugin
    {
        // The plugin component is destroyed by the SHCDE startup cleanup. The runtime, its
        // native detours and these subscriptions stay rooted by static fields; there is no
        // teardown path. Map changes only reset diagnostic state.
        private static DrawbridgeDeckRuntime runtime;
        private static IDisposable started, ended;
        private static ConfigEntry<bool> enabledEntry, detailEntry;

        private void Awake()
        {
            if (runtime != null) return;
            var log = Logger;
            enabledEntry = Config.Bind("General", "Enabled", true,
                "Treat lowered drawbridges in front of gatehouses as raised for players the gate does not admit. Gameplay relevant: all multiplayer peers must use the same value.");
            detailEntry = Config.Bind("Diagnostics", "DetailLog", true, "Log each closed deck and redirected gate portal once per map.");
            runtime = new DrawbridgeDeckRuntime(log) { Enabled = enabledEntry.Value, DetailLog = detailEntry.Value };
            CrusaderLibrary.Instance.LibraryLoaded += context =>
            {
                try { runtime.Install(context); }
                catch (Exception error) { Shared.DebugLogHelper.LogError(log, "Drawbridge deck closure inactive: " + error.Message); }
            };
            started = Shared.GameplaySessionLifecycle.SubscribeStarted(log, context =>
            {
                runtime.Enabled = BridgeAcceptance.ForcedEnabled ?? enabledEntry.Value;
                runtime.DetailLog = detailEntry.Value;
                runtime.BeginSession();
                Shared.DebugLogHelper.LogInfo(log, "Drawbridge deck closure map start: session=" + context.SessionId + ",editor=" + context.IsEditor +
                    ",installed=" + runtime.Installed + ",enabled=" + runtime.Enabled);
            });
            ended = Shared.MissionEvents.Ended.Subscribe(_ => Shared.DebugLogHelper.LogInfo(log, "Drawbridge deck closure map end: " + runtime.Summary()));
            BridgeAcceptance.TryStart(log, runtime);
            Shared.DebugLogHelper.LogInfo(log, "EnemyBridgePathTest registered: lowered gate drawbridges are closed for planning of players the gate does not admit.");
        }
    }
}
