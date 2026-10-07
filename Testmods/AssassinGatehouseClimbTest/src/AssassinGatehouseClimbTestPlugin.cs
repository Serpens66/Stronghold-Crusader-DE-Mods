using APIShared;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace AssassinGatehouseClimbTest
{
    [BepInPlugin(PluginGuid, "Assassin Gatehouse Climb Test", "0.1.0")]
    [BepInDependency("000shcdese", "2.9.0")]
    [BepInDependency("APIShared_Serp", "0.4.10")]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class AssassinGatehouseClimbTestPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "AssassinGatehouseClimbTest_Serp";
        private static Runtime runtime;
        private void Awake()
        {
            if (runtime != null) return;
            runtime = new Runtime(Logger);
            CrusaderLibrary.Instance.LibraryLoaded += runtime.OnLibraryLoaded;
        }

        // Rooted statically and by the native library, mission and tick publishers.
        // No plugin/component lifecycle can release the process-wide capability.
        private sealed class Runtime
        {
            private readonly ManualLogSource log;
            private readonly Dictionary<uint, UnitObservation> observations = new Dictionary<uint, UnitObservation>();
            private bool ready;
            private bool tickConfirmed;
            private bool tickFailureLogged;
            private int detailCount;
            private int previousTick;
            public Runtime(ManualLogSource logger) { log = logger; }
            public void OnLibraryLoaded(CrusaderLibraryLoadContext context)
            {
                if (ready) return;
                try
                {
                    AssassinPathAPI.SetDirectGatehouseClimbing(PluginGuid, true);
                    ApiShared.WhenReady(api =>
                    {
                        if (!api.TryGetMissionLifecycle(PluginGuid, out IMissionLifecycleCapability lifecycle,
                            out NativeCapabilityDiagnostic diagnostic) ||
                            !lifecycle.TryRegisterObserver("climb-test", OnStart, OnEnd, null, out diagnostic))
                            Log("Error", "Mission observation unavailable: " + diagnostic?.Reason);
                    });
                    GameTimeManagerAPI.Instance.OnTick += OnTick;
                    ready = true;
                    Log("Info", "READY: direct gatehouse climb eligibility enabled; hooks owned by APIShared; " +
                        "human cursor, native AI gate action, route search and reconstruction share the rule.");
                }
                catch (Exception ex) { Log("Error", "Initialization failed; gatehouse test unavailable: " + ex); }
            }
            private void OnStart(MissionLifecycleNotification notification)
            {
                observations.Clear(); detailCount = 0; tickConfirmed = false; tickFailureLogged = false;
                Log("Info", "MAP START after startup cleanup; awaiting simulation tick.");
            }
            private void OnEnd(MissionLifecycleNotification notification) { observations.Clear(); }
            private void OnTick(int tick)
            {
                if (!ready || tick == previousTick) return;
                previousTick = tick;
                try
                {
                    if (!tickConfirmed)
                    {
                        tickConfirmed = true;
                        Log("Info", "RUNTIME TICK confirmed after startup cleanup; tick=" + tick);
                    }
                    Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                    for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
                    {
                        ref GameUnit unit = ref units[spanIndex];
                        if (unit.r_AliveState != AliveState.IsAlive || unit.r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN) continue;
                        int currentTile = checked((int)unit.r_CurrentPositionTileId);
                        int nextTile = checked((int)unit.r_NextPositionTileId2);
                        bool gateCurrent = AssassinPathAPI.IsDirectGatehouseClimbEndpoint(currentTile);
                        bool gateNext = AssassinPathAPI.IsDirectGatehouseClimbEndpoint(nextTile);
                        int state = unit.r_AIState;
                        bool climbing = state >= 126 && state <= 129; // Audited Assassin physical states, no SE names exist.
                        if (!observations.TryGetValue(unit.r_GlobalId, out UnitObservation previous))
                        {
                            previous = new UnitObservation();
                            observations[unit.r_GlobalId] = previous;
                        }
                        bool climbedGate = previous.GateClimb || (climbing && (gateCurrent || gateNext));
                        bool relevantChange = previous.State != state || previous.Command != unit.r_AI_LastIssuedTribeCommand ||
                            previous.Tile != currentTile || previous.NextTile != nextTile;
                        if (detailCount < 150 && relevantChange && (climbing || gateCurrent || gateNext || previous.GateClimb))
                        {
                            detailCount++;
                            Log("Info", $"OBS tick={tick}, unitId={spanIndex + 1}, global={unit.r_GlobalId}, " +
                                $"player={unit.r_ControllableForPlayerId}, command={unit.r_AI_LastIssuedTribeCommand}, state={state}, " +
                                $"tile={currentTile}, next={nextTile}, target=({unit.r_TargetTilePositionX2},{unit.r_TargetTilePositionY2}), " +
                                $"gateCurrent={gateCurrent}, gateNext={gateNext}, physicalGateClimb={climbedGate}, " +
                                $"climbCompleted={previous.GateClimb && !climbing && gateCurrent}.");
                        }
                        previous.State = state; previous.Command = unit.r_AI_LastIssuedTribeCommand;
                        previous.Tile = currentTile; previous.NextTile = nextTile;
                        previous.GateClimb = climbing && climbedGate;
                    }
                }
                catch (Exception ex)
                {
                    if (tickFailureLogged) return;
                    tickFailureLogged = true;
                    Log("Error", "Read-only observation failed; shared climb rule remains active: " + ex);
                }
            }
            private void Log(string level, string message)
            {
                string text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                if (level == "Error") log.LogError(text); else log.LogInfo(text);
            }
            private sealed class UnitObservation
            {
                public int State = -1, Command = -1, Tile = -1, NextTile = -1;
                public bool GateClimb;
            }
        }
    }
}
