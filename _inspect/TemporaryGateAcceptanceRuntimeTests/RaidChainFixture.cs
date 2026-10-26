// Development-only fixture. Real classifier/retry body; native query, candidate ordering,
// SDK memory and command callback are controlled boundaries, NOT a Vanilla planner test.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static BugfixesAndQoL.RaidAttackFieldEvaluation;

internal unsafe sealed class RaidChainFixture
{
    private const int TargetBuildingIdOffset=0x622, TargetGlobalIdOffset=0x626, MaximumRetryCommandsPerTick=4;
    private readonly Dictionary<RaidGroupKey,RaidRetry> pendingRaidRetries=new();
    private readonly HashSet<int> rejected=new();
    private AttackResult fallbackResult;
    private string fallbackSummary, fallbackDetails;
    private bool issuingFallback;
    private int fallbackTribeId, fallbackBuildingId;
    private uint fallbackBuildingGlobalId;
    private int calls, role, selected, assertions;
    private bool validIdentity=true, commandIssued=true, storeTarget=true;
    private AttackCandidateSnapshot next;
    private Func<int,AttackCandidateSnapshot> nativeAnswer;
    private string freshness="nativeSearchObserved";
    private int sourcePcl=1;
    private bool registered;
    private sealed partial class BuildingQuery
    {
        internal enum EnemyGateSearchKind { BuildingApproach }
        internal interface IEnemyGatePathPolicy {
            bool HasPublishedMask {get;}
            int ResolveBuildingPlayer(int raw,int tribe);
            object EnterNativeSearch(int player,EnemyGateSearchKind kind);
            void ExitNativeSearch(object scope,EnemyGateSearchKind kind,bool completed,bool success);
        }
        private sealed class Policy:IEnemyGatePathPolicy {
            internal int Depth;
            public bool HasPublishedMask=>true;
            public int ResolveBuildingPlayer(int raw,int tribe)=>raw==2&&tribe==37?5:-1;
            public object EnterNativeSearch(int player,EnemyGateSearchKind kind) { if(player!=5)throw new Exception("wrong moving player");return ++Depth; }
            public void ExitNativeSearch(object token,EnemyGateSearchKind kind,bool completed,bool success) {
                if((int)token!=Depth||!completed||success)throw new Exception("wrong completion");Depth--;
            }
        }
        private static class EnemyGatePathPolicyBridge { internal static IEnemyGatePathPolicy Current; }
        private static class EnemyBridgeDiagnosticBridge {
            internal static object BeginSearch(string source,int player)=>null;
            internal static void EndSearch(object token,bool completed,int result){}
        }
        private Action body;
        private int sourceRegion, calls;
        private bool enabled;
        private void ObserveBuildingApproachBuilderWithMoat(IntPtr context,int tribe,int building,int requested,int region,int raw) {
            if(context!=new IntPtr(1)||tribe!=37||requested!=1||region!=sourceRegion||raw!=2)throw new Exception("native arguments changed");
            if(enabled&&((Policy)EnemyGatePathPolicyBridge.Current).Depth!=1)throw new Exception("native query outside gate scope");
            calls++;body();
        }
        private object BeginTemporaryAssassinScope(string source,int tribe,int player,int building,int region)=>null;
        private void EndTemporaryAssassinScope(object token,bool completed,int result){}
        private void ObserveEnemyGateAssassinBuilding(IEnemyGatePathPolicy p,int t,int b,int r,int raw){}
        private void TryLogDiagnosticFailure(string source,Exception e)=>throw new Exception(source,e);
        // The separate gate-attack freeze DEVDIAG capture is outside this fixture's contract; keep it neutral.
        private static class GateAttackFreezeCapture { internal const int Approach=0; internal struct Context {}
            internal static long Enter(int stage,Context context)=>0; internal static void Leave(long token,int result,bool completed) {} }
        private GateAttackFreezeCapture.Context FreezeGroupContext(int tribe,int building)=>default;
        /* ACTUAL_BUILDING_WRAPPER */
        internal void Run(int building,int region,bool active,Action callback) {
            var policy=new Policy();EnemyGatePathPolicyBridge.Current=active?policy:null;
            enabled=active;sourceRegion=region;body=callback;int before=calls;
            try {ObserveBuildingApproachBuilder(new IntPtr(1),37,building,1,region,2);}
            finally {EnemyGatePathPolicyBridge.Current=null;}
            if(calls!=before+1||policy.Depth!=0)throw new Exception("native call count or leaked gate scope");
        }
    }
    private readonly BuildingQuery query=new();
    private readonly List<string> outcomes=new();
    private enum AliveState { IsAlive=2 }
    [StructLayout(LayoutKind.Explicit, Size=1672)]
    private struct GameTribe { [FieldOffset(0x16)] public AliveState r_AliveState; }
    private sealed class GameTribeManagerAPI
    {
        internal static GameTribeManagerAPI Instance;
        internal GameTribe* Tribe;
        internal Func<int,int,int,bool> Command;
        internal bool TryGetTribeById(int id,out GameTribe* value) { value=Tribe; return id==37; }
        internal bool AttackBuildingEx(int tribe,int building,int global)=>Command(tribe,building,global);
    }
    private bool TryIdentifyRaidGroup(int tribe,out int player,out int group,out uint global)
    { player=5;group=role;global=validIdentity?9u:10u;return tribe==37; }
    private bool TryIsMeleeRaidGroup(int tribe,uint global,out string reason) { reason="fixture-melee";return true; }
    private bool TryReadOrderedRaidCandidates(RaidRetry retry,out List<BuildingTarget> candidates,out string reason)
    { candidates=null;reason="not-simulated";return false; }
    private bool IsRejectedTarget(RaidGroupKey key,int id,uint global)=>rejected.Contains(id);
    private bool IsLiveBuildingIdentity(int id,uint global,int owner)=>global==(uint)(id+100);
    private void RememberRejectedTarget(RaidGroupKey key,int id,uint global)=>rejected.Add(id);
    private void LogRetry(RaidRetry retry,string result,int id,uint global,int stored=0,uint storedGlobal=0)
    { outcomes.Add(result);selected=id; }
    /* ACTUAL_RETRY_TYPES */
    /* ACTUAL_RETRY */
    private void Check(bool value,string reason) { if(!value)throw new Exception(reason);assertions++; }
    private void Store(int id,uint global)
    { byte* p=(byte*)GameTribeManagerAPI.Instance.Tribe;*(ushort*)(p+TargetBuildingIdOffset)=(ushort)id;*(uint*)(p+TargetGlobalIdOffset)=global; }
    private AttackResult Evaluate(int building, bool pair=true, bool identity=true, long returned=1)
    {
        AttackCandidateSnapshot output=null;
        query.Run(building,sourcePcl,registered,()=>output=nativeAnswer==null?next:nativeAnswer(building));
        return BugfixesAndQoL.RaidAttackFieldEvaluation.Evaluate(output,pair,freshness,returned,identity,
            building,320800,t=>building,(a,b)=>b==a+1,out fallbackSummary);
    }
    private bool Issue(int tribe,int building,int global)
    {
        calls++;
        Check(issuingFallback&&fallbackTribeId==tribe&&fallbackBuildingId==building&&fallbackBuildingGlobalId==(uint)global,
            "actual retry must establish its synchronous target context before native boundary");
        if(storeTarget)Store(building,(uint)global);
        fallbackResult=Evaluate(building);fallbackDetails=fallbackSummary;
        return commandIssued;
    }
    private void Reset(bool point)
    {
        pendingRaidRetries.Clear();rejected.Clear();outcomes.Clear();calls=selected=0;
        validIdentity=commandIssued=storeTarget=true;freshness="nativeSearchObserved";
        nativeAnswer=null;
        next=new AttackCandidateSnapshot(true,point?1:0,point?new[]{new CandidateRecord(80100,80101,12)}:Array.Empty<CandidateRecord>());
        Store(100,200);
    }
    private void Queue()
    {
        var r=new RaidRetry(5,role,37,9,2,100,200,0,10);
        r.Candidates=new List<BuildingTarget>{new(101,201,20),new(102,202,32)};
        pendingRaidRetries.Add(r.Key,r);
    }
    internal static void Run(Action<bool,int> observeFinalRoute)
    {
        var t=new RaidChainFixture();
        var api=new GameTribeManagerAPI{Tribe=(GameTribe*)Marshal.AllocHGlobal(sizeof(GameTribe))};
        *api.Tribe=new GameTribe{r_AliveState=AliveState.IsAlive};api.Command=t.Issue;GameTribeManagerAPI.Instance=api;
        try
        {
            for(int role=0;role<6;role++)
            {
                t.role=role;
                // Provided fresh output: classification is independent of equal/different PCL.
                // The native building-search result is NOT inferred from either value.
                foreach(bool registered in new[]{false,true}) foreach(int sourcePcl in new[]{1,2})
                {
                    t.registered=registered;t.sourcePcl=sourcePcl;
                    t.Reset(true);
                    t.Check(t.Evaluate(100)==AttackResult.AttackPoint,"fresh valid pair retained");
                    t.ProcessRaidRetries();t.Check(t.calls==0,"valid target cannot invent a retry");
                    observeFinalRoute(true,role); // supplied detour; actual materialized-route observer below
                    observeFinalRoute(false,role); // same valid attack point, later failed route
                    t.Check(t.calls==0&&t.pendingRaidRetries.Count==0,"late failure has no automatic retarget in existing retry consumer");
                    t.Reset(false);t.Check(t.Evaluate(100)==AttackResult.NoAttackPoint,"fresh empty result");
                    t.Queue();t.next=new AttackCandidateSnapshot(true,1,new[]{new CandidateRecord(80100,80101,12)});
                    t.ProcessRaidRetries();t.Check(t.calls==1&&t.selected==101&&!t.issuingFallback,"actual retry selects valid replacement once and restores context");
                    observeFinalRoute(true,role);
                    t.Reset(true);t.freshness="missingPost";t.Queue();t.ProcessRaidRetries();
                    t.Check(t.calls==1&&t.selected==0&&t.outcomes.Contains("uncertain;vanillaPreserved"),"stale output cannot select a target or continue retries");
                    t.Reset(false);t.Queue();t.ProcessRaidRetries();
                    t.Check(t.calls==2&&t.outcomes.Contains("noTarget;storedTargetCleared"),"exhausted fresh-negative candidates clear matching failed target");
                }
            }
            foreach(bool issued in new[]{false,true})
            {
                t.Reset(true);t.commandIssued=issued;t.storeTarget=!issued;t.Queue();t.ProcessRaidRetries();
                t.Check(t.selected==0&&t.calls==1&&t.outcomes.Contains("uncertain;vanillaPreserved"),"skipped command or changed stored target preserves uncertain outcome");
            }
            t.Reset(true);t.Queue();t.validIdentity=false;t.ProcessRaidRetries();t.Check(t.calls==0,"reused tribe identity cannot issue fallback");
            t.Reset(true);t.Check(t.Evaluate(100,false)==AttackResult.Unknown&&t.Evaluate(100,true,false)==AttackResult.Unknown,"unpaired or replaced target cannot pass");
            t.Reset(false);t.Queue();
            t.nativeAnswer=id=>id==101?new AttackCandidateSnapshot(true,0,Array.Empty<CandidateRecord>()):
                new AttackCandidateSnapshot(true,1,new[]{new CandidateRecord(80100,80101,12)});
            t.ProcessRaidRetries();
            t.Check(t.calls==2&&t.selected==102&&t.rejected.Contains(101),"actual loop rejects first fresh empty candidate and selects second fresh valid candidate");
            observeFinalRoute(true,t.role);
            Console.WriteLine("PASS: "+t.assertions+" actual Raid classifier/retry assertions with final-route observation; native returns/candidate order simulated.");
        }
        finally { Marshal.FreeHGlobal((IntPtr)api.Tribe);GameTribeManagerAPI.Instance=null; }
    }
}
