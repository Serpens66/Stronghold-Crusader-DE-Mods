using APIShared;
using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.EventAPI;
using SHCDESE.API.LowLevel;
using System;
using UnityEngine;

namespace EnemyBridgePathTest
{
    [BepInPlugin("EnemyBridgePathTest_Serp", "Enemy Bridge Path Test", "0.1.0")]
    [BepInDependency("000shcdese", "2.7.1")]
    [BepInDependency("APIShared_Serp", "0.3.6")]
    [BepInDependency("BugfixesAndQoL_Serp")]
    public sealed class EnemyBridgePathTestPlugin : BaseUnityPlugin
    {
        // The plugin component is destroyed during startup. These registrations and
        // the static observer remain alive; map end only clears logical state.
        private static BridgeDiagnostics runtime;
        private static IDisposable started, ended, target, move, unit, movement, buildingSpawn, buildingDelete;
        private static int frame = -1;
        private void Awake()
        {
            if (runtime != null) return;
            runtime = new BridgeDiagnostics(Logger);
            CrusaderLibrary.Instance.LibraryLoaded += context => runtime.InitializeNative(context);
            if (!EnemyBridgeDiagnosticBridge.TryRegister(runtime))
                throw new InvalidOperationException("Bridge observer already registered");
            started = Shared.GameplaySessionLifecycle.SubscribeStarted(Logger, context => runtime.BeginMap(context));
            ended = Shared.MissionEvents.Ended.Subscribe(_ => runtime.End());
            target = TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable.Subscribe(runtime.TargetOrder);
            move = TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable.Subscribe(runtime.TribeMove);
            unit = UnitR3EventHooks.OnUnitMoveHere.Observable.Subscribe(runtime.UnitMove);
            movement = UnitR3EventHooks.OnUnitMovement.Observable.Subscribe(runtime.UnitMovement);
            buildingSpawn = BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(args => { if(BridgeSnapshot.IsGate(args.Building)||(int)args.Building==49) runtime.Trace.InvalidateBuildings(); });
            buildingDelete = BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(_ => runtime.Trace.InvalidateBuildings());
            Application.onBeforeRender += Render;
            Shared.DebugLogHelper.LogInfo(Logger,
                "EnemyBridgePathTest registered: diagnosis-only, no gate provider, passive native decision probes, no active edge mask.");
        }
        private static void Render()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            runtime?.Deferred();
        }
    }
}
