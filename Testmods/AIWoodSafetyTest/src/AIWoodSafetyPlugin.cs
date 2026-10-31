using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;

namespace AIWoodSafetyTest
{
    [BepInPlugin(Guid, "AI Wood Safety Test", Version)]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.9.2")]
    [BepInDependency("BugfixesAndQoL_Serp", "1.0.192")]
    [BepInDependency("AIBuildDiagnoseTest_Serp", "0.2.1")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class AIWoodSafetyPlugin : BaseUnityPlugin
    {
        public const string Guid = "AIWoodSafetyTest_Serp", Version = "0.1.0";
        private static ManualLogSource log;
        private static ConfigEntry<bool> build, death;
        private static WoodSafetyRuntime runtime;
        private static bool registered;
        private static readonly CompositeDisposable subscriptions = new CompositeDisposable();
        private void Awake()
        {
            if (registered) return; registered = true;
            log = Logger;
            build = Config.Bind("Safety", "ProtectNewHutsFromFortifications", true, "Reject AI woodsman sites within archer range of hostile towers/gatehouses. Read at mission start; SP only.");
            death = Config.Bind("Safety", "IgnoreDeadWorkersForCounter", true, "Undo only the current +10 hut-counter contribution of a provably dead AI woodcutter. Read at mission start; loaded counters unchanged.");
            CrusaderLibrary.Instance.LibraryLoaded += Loaded;
            Shared.DebugLogHelper.LogInfo(log, "AI Wood Safety Test loaded; both rules default on; configuration sampled between missions; SP only, editor/replay excluded.");
        }
        private static void Loaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try
            {
                runtime = new WoodSafetyRuntime(log, context);
                Shared.GameplaySessionLifecycle.SubscribeStarted(log, Start, () => runtime.End()).AddTo(subscriptions);
                Shared.MissionEvents.Loading.Subscribe(e => { if (e.Phase == APIShared.MissionInitializationPhase.BeforeLoad) runtime.End(); }).AddTo(subscriptions);
                BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(e => { if (e.Phase == EventHookPhase.Post && e.ReturnValue > 0 && e.ReturnValue <= int.MaxValue) runtime.BuildingChanged((int)e.ReturnValue); }).AddTo(subscriptions);
                BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(e => runtime.BuildingChanged(e.BuildingId)).AddTo(subscriptions);
                BuildingR3EventHooks.OnBuildingCapture.Observable.Subscribe(e => runtime.BuildingChanged(e.BuildingId)).AddTo(subscriptions);
                BuildingR3EventHooks.OnPlacementValidation.Observable.Subscribe(e => runtime.ValidatePlacement(e)).AddTo(subscriptions);
                GameTimeManagerAPI.Instance.OnTick += Tick;
                UnityEngine.Application.onBeforeRender += Drain;
                AppDomain.CurrentDomain.ProcessExit += Exit;
                runtime.Info("READY publisher=MissionLifecycle/OnTick/onBeforeRender; processRooted=true; runtimeGameMembers=none");
            }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI_WOOD_SAFETY_INITIALIZATION_FAILED: " + ex); }
        }
        private static void Start(Shared.GameplaySessionStartedContext context) => runtime.Start(context, build.Value, death.Value);
        private static void Tick(int tick) => runtime?.Tick(tick);
        private static void Drain() => runtime?.Drain();
        private static void Exit(object sender, EventArgs args) => runtime?.FinishAndDrain();
    }
}
