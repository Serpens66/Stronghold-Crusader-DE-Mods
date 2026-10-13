using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using UnityEngine;
using APIShared;
namespace BugfixesAndQoL.GateRouting
{
    // TEMP_GATE_MIGRATION_DIAGNOSTICS: local diagnostic options; never synchronized gameplay settings.
    internal static class GateRoutingFeature
    {
        private static BugfixesAndQoLViewModel settings;
        private static ManualLogSource log;
        private static EnemyGatePathfindingRuntime runtime;
        private static CrusaderLibraryLoadContext context;
        private static bool initialized, configured, map, editor, active, attempted, detailed, macemen;
        private static long rulesVersion;
        private static bool ownRule;
        private static readonly object StateGate=new object();
        internal static bool IsActive => System.Threading.Volatile.Read(ref active);
        private static int frame=-1;
        private static readonly List<IDisposable> subscriptions=new List<IDisposable>();
        private readonly struct AccessMemo
        {
            internal AccessMemo(uint global,int owner,int capturer,bool baseline,bool blocked,long version)
            {Global=global;Owner=owner;Capturer=capturer;Baseline=baseline;Blocked=blocked;Version=version;}
            internal readonly uint Global;internal readonly int Owner,Capturer;internal readonly bool Baseline,Blocked;internal readonly long Version;
        }
        private static readonly Dictionary<long,AccessMemo> accessMemo=new Dictionary<long,AccessMemo>();
        private static long ruleErrorCount;
        private static readonly HashSet<string> ruleErrors=new HashSet<string>(StringComparer.Ordinal);
        internal static void Configure(ManualLogSource logger,ConfigFile config,BugfixesAndQoLViewModel options)
        {
            if(configured)return;configured=true;log=logger;settings=options;
            detailed=config.Bind("GateMigrationDiagnostics","DetailedDiagnostics",true,"TEMP_GATE_MIGRATION_DIAGNOSTICS: retain complete gate diagnosis for migration acceptance; restart required.").Value;
            macemen=config.Bind("GateMigrationDiagnostics","MacemenDiagnostics",false,"TEMP_MACEMEN_GATE_DIAGNOSTICS: optional compact command diagnosis; restart required.").Value;
            subscriptions.Add(Shared.GameplaySessionLifecycle.SubscribeStarted(log,c=>{map=true;editor=c.IsEditor;Refresh();}));
            subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(_=>{map=false;Refresh();}));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingCapture.Observable.Subscribe(a=>{if(active)runtime?.ObserveBuildingCapture(a);}));
            if(detailed||macemen) {
                subscriptions.Add(TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable.Subscribe(a=>{if(active)runtime?.ObserveTargetOrder(a);}));
                subscriptions.Add(TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable.Subscribe(a=>{if(active)runtime?.ObserveTribeMove(a);}));
                subscriptions.Add(UnitR3EventHooks.OnUnitMoveHere.Observable.Subscribe(a=>{if(active)runtime?.ObserveUnitMove(a);}));
            }
            Application.onBeforeRender+=Deferred;
            settings.SettingChanged+=Changed;
        }
        internal static void Initialize(CrusaderLibraryLoadContext library,bool compatible)
        {
            if(initialized)return;initialized=true;
            if(!compatible) {Shared.DebugLogHelper.LogError(log,"Gate routing inactive: unsupported native layout");return;}
            var owner=UnitCommands.UnitCommandPathAPI.Runtime;
            if(owner==null||owner.disposed||owner.pathBuilderDetour?.Committed!=true||
                owner.attackApproachFloodBuilderDetour?.Committed!=true||owner.buildingApproachBuilderDetour?.Committed!=true||
                owner.buildingCandidateConsumerDetour?.Committed!=true||owner.cursorMoveStagerDetour?.Committed!=true||
                owner.regionPairReachabilityDetour?.Committed!=true)
            {Shared.DebugLogHelper.LogError(log,"Gate routing inactive: required existing search-hook owner is incomplete");return;}
            context=library;
            GameTimeManagerAPI.Instance.OnTick+=Tick;
            Refresh();
        }
        private static void Changed(string name)
        {
            if(name==nameof(BugfixesAndQoLViewModel.EnableMod)||name==nameof(BugfixesAndQoLViewModel.EnableEnemyGatePathfindingFix))Refresh();
        }
        private static void Tick(int tick) { lock(StateGate) { RefreshCore();if(active)runtime?.OnGameTick(); } }
        private static void Deferred()
        {
            int current=Time.frameCount;if(current==frame)return;frame=current;
            lock(StateGate) { RefreshCore();if(active)runtime?.ProcessDeferredDiagnostics(); }
        }
        private static void Refresh() { lock(StateGate) RefreshCore(); }
        private static void RefreshCore()
        {
            if(settings==null)return;
            bool nextOwn=settings.EnableMod&&settings.EnableEnemyGatePathfindingFix;
            long nextVersion=GateAccessRules.Version;
            bool wanted=map&&settings.EnableMod&&(nextOwn||GateAccessRules.HasRegistrations)&&context!=null;
            if(active&&(!wanted||nextOwn!=ownRule||nextVersion!=rulesVersion)) {active=false;runtime.EndMap("Gate setting/rule/map transition");if(ruleErrorCount!=0)Shared.DebugLogHelper.LogError(log,"Gate contribution failures: count="+ruleErrorCount+",causes="+ruleErrors.Count);active=false;}
            ownRule=nextOwn;rulesVersion=nextVersion;
            if(!wanted||active)return;
            if(runtime==null&&!attempted) {
                attempted=true;
                try {
                    // The installed legacy package is removed by the migration driver before startup.
                    if(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("EnemyGatePathfindingTest_Serp"))
                        throw new InvalidOperationException("Legacy gate testmod is loaded; disable it before integrated gate initialization");
                    var candidate=new EnemyGatePathfindingRuntime(log,detailed,macemen);
                    candidate.InitializeNative(context,true);runtime=candidate;
                }catch(Exception ex){Shared.DebugLogHelper.LogError(log,"Gate routing initialization failed; unrelated mainmod features remain active: "+ex);return;}
            }
            if(runtime==null)return;
            ruleErrors.Clear();ruleErrorCount=0;accessMemo.Clear();runtime.BeginMap(editor);active=true;
            Shared.DebugLogHelper.LogInfo(log,"TEMP_GATE_MIGRATION_DIAGNOSTICS provider=BugfixesAndQoL; nativeOwner=APIShared; hostFix="+ownRule+"; DetailedDiagnostics="+detailed+"; MacemenDiagnostics="+macemen+"; accessRulesVersion="+rulesVersion);
        }
        internal static bool IsBlocked(int player,int gate,uint global,int owner,int capturer,bool defaultBlocked)
        {
            bool allowed=!ownRule||!defaultBlocked;
            if(gate<=0||global==0||player<1||player>8)return !allowed;
            long key=((long)gate<<4)|unchecked((uint)player);long revision=GateAccessRules.Version;
            if(accessMemo.TryGetValue(key,out AccessMemo prior)&&prior.Global==global&&prior.Owner==owner&&prior.Capturer==capturer&&prior.Baseline==allowed&&prior.Version==revision)return prior.Blocked;
            bool blocked=!GateAccessRules.Evaluate(new GateAccessQuery(player,gate,global,owner,capturer,allowed),ReportRuleError);
            if(revision!=GateAccessRules.Version)throw new InvalidOperationException("Gate rules invalidated during snapshot evaluation");
            accessMemo[key]=new AccessMemo(global,owner,capturer,allowed,blocked,revision);return blocked;
        }
        private static void ReportRuleError(string owner,string id,Exception ex)
        {
            ruleErrorCount++;
            string key=owner+"/"+id+"/"+ex.GetType().FullName;
            if(ruleErrors.Add(key))Shared.DebugLogHelper.LogError(log,"Gate access contribution ignored: "+key+"; "+ex.Message);
        }
    }
}
