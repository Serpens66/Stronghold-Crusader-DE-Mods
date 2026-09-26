using System;
using APIShared;
using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Input;
using SHCDESE.Interop;
using UnityEngine;

namespace KeepCampfireGroundProbe
{
    [BepInPlugin(Guid, "Keep Campfire Ground Probe", Version)]
    [BepInDependency("000shcdese", "2.10.1")]
    [BepInDependency("APIShared_Serp", "0.3.6")]
    public sealed class KeepCampfireGroundProbePlugin : BaseUnityPlugin
    {
        public const string Guid = "KeepCampfireGroundProbe_Serp";
        public const string Version = "0.1.0";

        private static ManualLogSource log;
        private static GroundProbeRuntime runtime;
        private static IMissionLifecycleCapability lifecycle;

        private void Awake()
        {
            log = Logger;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            Write("bootstrap: waiting for Script Extender; F8 requests a manual snapshot in a map");
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext _)
        {
            if (runtime != null)
                return;

            try
            {
                if (!ApiShared.Current.TryGetMissionLifecycle(Guid, out lifecycle, out var diagnostic))
                    throw new InvalidOperationException("Mission lifecycle unavailable: " + diagnostic?.Reason);

                // Published events and this static field outlive BaseUnityPlugin startup cleanup.
                GroundProbeRuntime candidate = new GroundProbeRuntime(Write);
                runtime = candidate;
                if (!lifecycle.TryRegisterObserver(Guid + ".runtime", candidate.OnStart,
                    candidate.OnEnd, candidate.OnInitialization, out diagnostic))
                    throw new InvalidOperationException("Mission lifecycle registration failed: " + diagnostic?.Reason);

                BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(candidate.OnBuildingSpawn);
                InputR3EventHooks.OnKeyDown.Observable.Subscribe(candidate.OnKeyDown);
                GameTimeManagerAPI.Instance.OnTick += candidate.OnTick;
                Application.onBeforeRender += candidate.OnBeforeRender;
                Write("READY: read-only subscriptions installed; awaiting post-cleanup mission and render markers");
            }
            catch (Exception ex)
            {
                // A failed partial registration is not rolled back after publication.
                Write("initialization failed: " + ex);
            }
        }

        private static void Write(string message)
        {
            log?.LogInfo("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + message);
        }
    }
}
