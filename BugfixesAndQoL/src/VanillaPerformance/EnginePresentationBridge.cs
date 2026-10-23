using System;
using System.Reflection;
using System.Threading;
using BepInEx.Logging;
using HarmonyLib;
using R3;

namespace BugfixesAndQoL.VanillaPerformance
{
    // Functional managed bridge. Hooks and lifecycle subscriptions are permanent;
    // session objects are retired, never reused across load attempts.
    internal static partial class EnginePresentationBridge
    {
        internal sealed class Session
        {
            internal AchievementDataOptimization.Values Pending;
            internal long MeritDemandUntil, MeritLastAttempt;
            internal int MeritRequested;
            internal MeritDataLockOptimization.Snapshot MeritCache;
        }
        private static readonly Harmony harmony = new Harmony("BugfixesAndQoL_Serp.VanillaPerformance.EngineBridge");
        private static Session session;
        private static ManualLogSource log;
        private static int enabled, mainThread;
        private static volatile bool runValid, saveValid;
        private static bool installed;
        internal static object Gate { get; private set; }
        internal static Session Current => Volatile.Read(ref session);
        internal static bool Active => Volatile.Read(ref enabled) != 0 && runValid && saveValid;
        internal static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == mainThread;
        internal static void Install(ManualLogSource logger)
        {
            if (installed) return; installed = true; log = logger;
            mainThread = Thread.CurrentThread.ManagedThreadId;
            try
            {
                var field = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
                if (field == null || field.FieldType != typeof(object)) throw new MissingFieldException("EngineInterface.threadLock");
                Gate = field.GetValue(null) ?? throw new InvalidOperationException("Engine lock is null");
                // Before any patch is active, validate both original IL bodies. A
                // partial installation is harmless while enabled remains zero.
                ValidateOriginal(typeof(EngineInterface).GetMethod(nameof(EngineInterface.run), new[] { typeof(bool) }));
                ValidateOriginal(typeof(EngineInterface).GetMethod(nameof(EngineInterface.SaveSaveGame)));
                var transpiler = new HarmonyMethod(typeof(EnginePresentationBridge), nameof(Transpile))
                { priority = Priority.First, before = new[] { "PerfProbe_Serp.Timing" } };
                harmony.Patch(typeof(EngineInterface).GetMethod(nameof(EngineInterface.run), new[] { typeof(bool) }), transpiler: transpiler);
                harmony.Patch(typeof(EngineInterface).GetMethod(nameof(EngineInterface.SaveSaveGame)), transpiler: transpiler);
                AchievementDataOptimization.Install(harmony);
                Shared.MissionEvents.Loading.Subscribe(_ => Retire());
                Shared.MissionEvents.Ended.Subscribe(_ => Retire());
                Shared.GameplaySessionLifecycle.SubscribeStarted(log, _ =>
                {
                    Retire(); Volatile.Write(ref session, new Session());
                });
                Shared.DebugLogHelper.LogInfo(log, "VANILLA_PERF_BRIDGE_INSTALLED: managed run/save lock scopes validated; no native hook.");
            }
            catch (Exception error) { runValid = saveValid = false; Report(error); }
        }
        internal static void SetEnabled(bool value)
        {
            // Stop new producers before flushing; an already queued value still
            // belongs to this map and must survive turning the option off.
            Volatile.Write(ref enabled, 0);
            if (Gate != null) lock (Gate) { AchievementDataOptimization.Flush(Current); }
            MeritDataLockOptimization.Invalidate();
            if (value && runValid && saveValid) Volatile.Write(ref enabled, 1);
        }
        private static void Retire()
        {
            Interlocked.Exchange(ref session, null);
            MeritDataLockOptimization.Invalidate();
        }
        private static void EnteredRun()
        {
            // Independent of the option: other features rely on the scope as a pure marker.
            SimulationRunScope.Enter();
            if (Active) AchievementDataOptimization.Flush(Current);
        }
        private static void BeforePublish()
        {
            SimulationRunScope.Exit();
            if (Active) MeritDataLockOptimization.RefreshRequested();
        }
        private static void BeforeSave()
        {
            // Save cannot continue past a failed flush. The original native call
            // would also fail rather than silently write older achievement data.
            AchievementDataOptimization.Flush(Current);
        }
        internal static void Report(Exception error) => Shared.DebugLogHelper.LogErrorLimited(log,
            "vanilla-performance-engine-bridge", "VANILLA_PERF_BRIDGE: " + error);
    }
}
