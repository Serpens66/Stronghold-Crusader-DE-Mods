using APIShared.Buildings;
using APIShared.Simulation;
using System;
internal static partial class Program
{
    private static void PeaceTimeCivilianRuntimeTests()
    {
        var vm = new BugfixesAndQoL.BugfixesAndQoLViewModel();
        Func<BugfixesAndQoL.PeaceTimeCivilianProximityFeature> create = () =>
        {
            BepInEx.Bootstrap.Chainloader.PluginInfos.Clear();
            EnemyProximityPolicies.ExtraRegistered = false;
            EnemyProximityPolicies.QueryDiagnostic.State = APIShared.NativeCapabilityState.Available;
            PeaceTimeState.Diagnostic.State = APIShared.NativeCapabilityState.Available;
            APIShared.ApiShared.Missions.Context = null;
            Shared.DebugLogHelper.PeaceWarnings = 0;
            vm = new BugfixesAndQoL.BugfixesAndQoLViewModel();
            return new BugfixesAndQoL.PeaceTimeCivilianProximityFeature(new BepInEx.Logging.ManualLogSource(), vm);
        };
        Action<long> start = id =>
        {
            APIShared.ApiShared.Missions.Context = new APIShared.MissionContext { SessionId = id };
            Shared.GameplaySessionLifecycle.Started(new Shared.GameplaySessionStartedContext { SessionId = id });
        };
        Func<EnemyProximityDecision> query = () => EnemyProximityPolicies.Callback(default);
        var feature = create();
        Check(!EnemyProximityPolicies.Registration.Enabled && !vm.Locked, "new contributor is dormant outside a mission");
        start(1);
        Check(EnemyProximityPolicies.Registration.Enabled && vm.Locked, "completed start enables the frozen host contribution and locks local edits");
        PeaceTimeState.Available = true; PeaceTimeState.Active = true;
        var decision = query();
        Check(decision.CivilianRadius == 3 && decision.MilitaryRadius == null && !decision.Block,
            "actual contributor supplies only the civilian limit");
        vm.EnablePeaceTimeGameplayFixes = true; vm.EnableClientFeatures = false;
        Check(query().CivilianRadius == 3, "actual contributor is independent of SP extension and local client features");
        PeaceTimeState.Active = false;
        Check(query().CivilianRadius == null, "native peace expiry immediately removes the cap");
        PeaceTimeState.Active = true; PeaceTimeState.Available = false;
        Check(query().CivilianRadius == null, "unavailable peace source never grants the reduction");
        PeaceTimeState.Available = true;
        int reads = PeaceTimeState.Reads;
        APIShared.ApiShared.Missions.Context = new APIShared.MissionContext { SessionId = 2 };
        Check(query().CivilianRadius == null && PeaceTimeState.Reads == reads, "old mission snapshot cannot affect a replacement world");
        APIShared.ApiShared.Missions.Context = null;
        Check(query().CivilianRadius == null && PeaceTimeState.Reads == reads, "menu does not read a stale peace flag");
        APIShared.ApiShared.Missions.Context = new APIShared.MissionContext { SessionId = 1 };
        vm.EnableMod = false; vm.EnablePeaceTimeCivilianProximityReduction = false;
        vm.Change("EnablePeaceTimeCivilianProximityReduction");
        Check(query().CivilianRadius == 3, "authoritative snapshot writes do not alter the frozen active-mission rule");
        Shared.GameplaySessionLifecycle.Ended();
        Check(!EnemyProximityPolicies.Registration.Enabled && !vm.Locked, "mission end disables registration and unlocks next-game edits");
        start(3);
        Check(!EnemyProximityPolicies.Registration.Enabled && query().CivilianRadius == null,
            "next mission captures the changed host/option values");

        feature = create();
        BepInEx.Bootstrap.Chainloader.PluginInfos.Add("ExtraFeatures_Serp", new object());
        start(4);
        Check(!EnemyProximityPolicies.Registration.Enabled && query().CivilianRadius == null,
            "legacy ExtraFeatures direct subscriber disables only the new cap");
        vm.Change("EnableMod");
        Check(Shared.DebugLogHelper.PeaceWarnings == 1, "legacy compatibility warning is emitted once");
        EnemyProximityPolicies.ExtraRegistered = true;
        vm.Change("EnableMod");
        Check(EnemyProximityPolicies.Registration.Enabled && query().CivilianRadius == 3,
            "successful shared EF registration restores coexistence even without an active EF radius override");

        feature = create();
        PeaceTimeState.Diagnostic.State = APIShared.NativeCapabilityState.ValidationFailed;
        start(5); vm.Change("EnableMod"); vm.Change("EnableMod");
        Check(Shared.DebugLogHelper.PeaceWarnings == 1, "unavailable native service is reported once instead of failing silently");
        GC.KeepAlive(feature);
    }
}
