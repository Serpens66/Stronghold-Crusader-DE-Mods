using System;
using APIShared;
using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;

namespace KeepCampfireGroundPreserveTest
{
    [BepInPlugin(Guid, "Keep Campfire Ground Preserve Test", Version)]
    [BepInDependency("000shcdese", "2.10.1")]
    [BepInDependency("APIShared_Serp", "0.3.6")]
    public sealed class KeepCampfireGroundPreserveTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "KeepCampfireGroundPreserveTest_Serp";
        public const string Version = "0.1.0";

        private static ManualLogSource log;
        private static GroundPreserveRuntime runtime;
        private static CampgroundNativeHook nativeHook;
        private static TerrainPhaseDiagnostic terrainPhase;
        private static IMissionLifecycleCapability lifecycle;

        private void Awake()
        {
            log = Logger;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            Write("bootstrap: awaiting native library and Script Extender lifecycle");
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            GroundPreserveRuntime candidate = new GroundPreserveRuntime(Write, Error);
            runtime = candidate;
            try
            {
                if (!ApiShared.Current.TryGetMissionLifecycle(Guid, out lifecycle,
                    out var diagnostic))
                    throw new InvalidOperationException("Mission lifecycle unavailable: " +
                        diagnostic?.Reason);
                if (!lifecycle.TryRegisterObserver(Guid + ".runtime", candidate.OnStart,
                    candidate.OnEnd, candidate.OnInitialization, out diagnostic))
                    throw new InvalidOperationException("Mission lifecycle registration failed: " +
                        diagnostic?.Reason);

                BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(candidate.OnBuildingSpawn);
                BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(candidate.OnBuildingDelete);
                GameTimeManagerAPI.Instance.OnTick += candidate.OnTick;
                Write("read-only lifecycle and building observers installed");
            }
            catch (Exception ex)
            {
                Error("observer installation failed; native modification disabled: " + ex);
                return;
            }

            try
            {
                nativeHook = CampgroundNativeHook.TryCreate(context, log, Write);
                candidate.SetHook(nativeHook);
            }
            catch (Exception ex)
            {
                Error("native graphic suppression unavailable; read-only diagnosis remains: " + ex);
            }
            if (nativeHook != null)
            {
                try
                {
                    terrainPhase = TerrainPhaseDiagnostic.TryCreate(context, log, Write);
                    candidate.SetTerrainPhase(terrainPhase);
                }
                catch (Exception ex)
                {
                    Error("terrain-phase diagnosis unavailable; campground test remains active: " + ex);
                }
            }
        }

        private static void Write(string message) =>
            log?.LogInfo("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + message);

        private static void Error(string message) =>
            log?.LogError("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + message);
    }
}
