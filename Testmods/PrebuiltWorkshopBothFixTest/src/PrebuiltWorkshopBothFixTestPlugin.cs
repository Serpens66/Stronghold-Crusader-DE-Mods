using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using System;

namespace PrebuiltWorkshopBothFixTest
{
    [BepInDependency("000shcdese", "2.9.0")]
    [BepInDependency("APIShared_Serp", "0.4.2")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class PrebuiltWorkshopBothFixTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "PrebuiltWorkshopBothFixTest_Serp";
        public const string Name = "Prebuilt Workshop Both Fix Test";
        public const string Version = "0.1.0";

        private static ManualLogSource log;
        private static PrebuiltWorkshopBothFixTestRuntime runtime;
        private static IDisposable nativeStartSubscription;
        private static IDisposable sessionSubscription;
        private static IDisposable endSubscription;
        private static IDisposable spawnSubscription;
        private static bool tickRegistered;
        private static bool libraryRegistered;

        private void Awake()
        {
            log = Logger;
            Shared.DebugLogHelper.LogInfo(log, $"{Name} {Version} loaded; waiting for native library.");
            if (libraryRegistered) return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            libraryRegistered = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(log, Name, requireCurrentVersion: true))
                return;

            IDisposable candidateNativeStart = null;
            IDisposable candidateSession = null;
            IDisposable candidateEnd = null;
            IDisposable candidateSpawn = null;
            try
            {
                var candidate = new PrebuiltWorkshopBothFixTestRuntime(log, context);
                candidateNativeStart = Shared.MissionEvents.NativeStart.Subscribe(candidate.OnNativeStart);
                candidateSession = Shared.GameplaySessionLifecycle.SubscribeStarted(log, candidate.OnSessionStarted);
                candidateEnd = Shared.MissionEvents.Ended.Subscribe(_ => candidate.OnSessionEnded());
                candidateSpawn = BuildingR3EventHooks.OnBuildingSpawn.Observable
                    .Where(e => e.Phase == EventHookPhase.Post)
                    .Subscribe(candidate.OnBuildingSpawn);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                tickRegistered = true;

                nativeStartSubscription = candidateNativeStart;
                sessionSubscription = candidateSession;
                endSubscription = candidateEnd;
                spawnSubscription = candidateSpawn;
                runtime = candidate;
                Shared.DebugLogHelper.LogInfo(log, "Prebuilt workshop diagnosis active; runtime is rooted in extender events.");
            }
            catch (Exception ex)
            {
                if (tickRegistered)
                {
                    GameTimeManagerAPI.Instance.OnTick -= OnTick;
                    tickRegistered = false;
                }
                candidateSpawn?.Dispose();
                candidateEnd?.Dispose();
                candidateSession?.Dispose();
                candidateNativeStart?.Dispose();
                Shared.DebugLogHelper.LogError(log, "Prebuilt workshop diagnosis could not initialize: " + ex);
            }
        }

        private static void OnTick(int tick) => runtime?.OnTick(tick);
    }
}
