using System;
using System.IO;
using System.Threading;
using APIShared;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace UiAcceptance
{
    // Scripted acceptance runs. Without Enabled=true and a scenario file the plugin only logs once.
    // Lifetime: everything is rooted in statics; the driver runs through APIShared's process-lived
    // UI work service, which survives the startup destruction of early plugin components.
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.49.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Spectator_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("CastlePlanner_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("RandomEvents_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class UiAcceptancePlugin : BaseUnityPlugin
    {
        public const string Guid = "UiAcceptance_Serp", Name = "UiAcceptance", Version = "0.1.0";
        internal static ManualLogSource Log;
        internal static Harmony Harmony;
        internal static IMissionLifecycleCapability Missions;
        internal static long StartedSession, EndedSession;
        internal static MissionContext LastContext;
        internal static int Ticks;
        private static bool initialized;

        private void Awake()
        {
            if (initialized) return;
            initialized = true;
            Log = Logger;
            AcceptLog.Attach(Logger);
            ConfigEntry<bool> enabled = Config.Bind("Run", "Enabled", false, "Runs the scenario once at startup, then resets itself to false.");
            ConfigEntry<string> file = Config.Bind("Run", "Scenario", "", "Scenario JSON: absolute or relative to BepInEx/config/UiAcceptance.");
            ConfigEntry<string> label = Config.Bind("Run", "RunLabel", "", "Label written into every ACCEPT line.");
            if (!enabled.Value) { AcceptLog.Info("UiAcceptance idle (Run.Enabled=false)."); return; }
            enabled.Value = false; // One attempt per configured run, also after a crash.
            Config.Save();
            try
            {
                string path = Path.IsPathRooted(file.Value) ? file.Value : Path.Combine(Paths.ConfigPath, "UiAcceptance", file.Value);
                Scenario scenario = Scenario.Read(path);
                if (!scenario.Enabled) { AcceptLog.Info("Scenario disabled: " + path); return; }
                AcceptLog.RunId = string.IsNullOrEmpty(label.Value) ? scenario.Series : label.Value;
                Harmony = new Harmony(Guid);
                Steps.RegisterAll();
                var api = ApiShared.ForMod(Guid);
                if (!api.TryGetMissionLifecycle(out Missions, out var diagnostic) ||
                    !Missions.TryRegisterObserver("acceptance", OnStart, OnEnd, null, out diagnostic))
                    throw new InvalidOperationException("Mission lifecycle unavailable: " + diagnostic.Reason);
                SHCDESE.API.GameTimeManagerAPI.Instance.OnTick += tick => Interlocked.Increment(ref Ticks);
                if (!api.Ui.TryRegisterBeforeRender("driver", Runner.Tick, out var frame, out string reason, 1000))
                    throw new InvalidOperationException(reason);
                Runner.Start(scenario, frame);
            }
            catch (Exception error) { AcceptLog.Result("INIT", false, error.ToString()); }
        }

        private static void OnStart(MissionLifecycleNotification notification)
        {
            if (notification.IsReplay) return;
            LastContext = notification.Context;
            Interlocked.Exchange(ref StartedSession, notification.Context.SessionId);
            AcceptLog.Info("Mission started session=" + notification.Context.SessionId + " save=" + notification.Context.IsSave +
                " editor=" + notification.Context.IsEditor + " mode=" + notification.Context.Mode.ToDiagnosticString());
        }

        private static void OnEnd(MissionLifecycleNotification notification)
        {
            Interlocked.Exchange(ref EndedSession, notification.Context?.SessionId ?? 0);
            AcceptLog.Info("Mission ended session=" + notification.Context?.SessionId + " reason=" + notification.EndReason);
        }
    }
}
