using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
namespace EnemyBridgePathTest
{
    internal sealed unsafe class BridgeDecisionTrace
    {
        internal readonly struct Arguments : IEquatable<Arguments>
        {
            private readonly int a,b,c,d,e,f;
            internal Arguments(int a,int b,int c,int d,int e,int f) {this.a=a;this.b=b;this.c=c;this.d=d;this.e=e;this.f=f;}
            internal int this[int i] => i==0?a:i==1?b:i==2?c:i==3?d:i==4?e:f;
            public bool Equals(Arguments other) => a==other.a&&b==other.b&&c==other.c&&d==other.d&&e==other.e&&f==other.f;
            public override bool Equals(object other) => other is Arguments value&&Equals(value);
            public override int GetHashCode() => unchecked((((((a*397)^b)*397^c)*397^d)*397^e)*397^f);
            public override string ToString() => a+"/"+b+"/"+c+"/"+d+"/"+e+"/"+f;
        }
        internal readonly struct AttackStamp : IEquatable<AttackStamp>
        {
            internal readonly uint Global, TargetGlobal;
            internal readonly int Owner, Leader, State, Phase, Target;
            internal AttackStamp(uint global,int owner,int leader,int state,int phase,int target,uint targetGlobal)
            {Global=global;Owner=owner;Leader=leader;State=state;Phase=phase;Target=target;TargetGlobal=targetGlobal;}
            public bool Equals(AttackStamp b) => Global==b.Global&&Owner==b.Owner&&Leader==b.Leader&&State==b.State&&Phase==b.Phase&&Target==b.Target&&TargetGlobal==b.TargetGlobal;
            public override string ToString() => "global="+Global+",owner="+Owner+",leader="+Leader+",state16="+State+",phase16="+Phase+",retainedTarget16="+Target+",targetGlobal="+TargetGlobal;
        }
        internal sealed class Scope
        {
            internal Scope Parent, Next;
            internal BridgeNativeDefinition.Site Site;
            internal long Id,Session,Regions;
            internal Arguments Args;
            internal AttackStamp PreStamp;
            internal Arguments PrePlan;
            internal int PlanningPlayer;
            internal long Commands, CandidateBuilds;
            internal Arguments AccessInput;
            internal bool AccessValid;
            internal int AccessNative,AccessEffective; internal bool AccessRegionObserved;
            internal readonly List<AccessObservation> Accesses=new List<AccessObservation>();
            internal int SelectedUnit;
            internal uint SelectedGlobal;
            internal bool Rebuilt;
            internal readonly List<TableImage> SelectionTables=new List<TableImage>();
            internal readonly List<SelectionEvidence> Selections=new List<SelectionEvidence>();
            internal TableImage LadderBefore;
            internal readonly List<SelectionEvidence> LadderCommands=new List<SelectionEvidence>();
            internal readonly HashSet<int> RegionSamples=new HashSet<int>();
            internal bool Detailed;
        }
        internal readonly struct AccessObservation
        {
            internal readonly long Op,Result; internal readonly int Mode,Native,Effective,Calls; internal readonly bool Observed;
            internal readonly Arguments Input;
            internal AccessObservation(Scope s,long result) {Op=s.Id;Result=result;Mode=s.Site.Rva==0xCF400?1:0;Input=s.AccessInput;Native=s.AccessNative;Effective=s.AccessEffective;Observed=s.AccessRegionObserved;Calls=(int)s.Regions;}
            public override string ToString() => Op+"/"+Mode+"/"+Input+"/"+Result+"/"+(Observed?1:0)+"/"+Native+"/"+Effective+"/"+Calls;
        }
        private sealed class DecisionState {internal long Id,Session,Root,AccessSignature;internal Arguments Plan;internal AccessObservation[] Accesses;}
        private readonly DecisionState[] decisions=new DecisionState[9];
        private readonly object decisionGate=new object();
        private long decisionId;
        internal Action<long> PromoteCommandChain;
        internal long DecisionFor(int player,Arguments entry)
        {
            if(player<1||player>8)return 0;
            lock(decisionGate)
            {
                var value=decisions[player];if(value==null)return 0;
                if(value.Session!=session||!value.Plan.Equals(entry)) {decisions[player]=null;Emit("decision-state-ended","definition="+value.Id+",player="+player+",reason=plan-or-session-changed");return 0;}
                return value.Id;
            }
        }
        private long DecisionRootFor(int player,long id)
        {if(player<1||player>8||id==0)return 0;lock(decisionGate){var value=decisions[player];return value!=null&&value.Id==id&&value.Session==session?value.Root:0;}}
        private static bool SameAccesses(AccessObservation[] prior,List<AccessObservation> current)
        {
            if(prior==null||prior.Length!=current.Count)return false;
            for(int i=0;i<prior.Length;i++) {var a=prior[i];var b=current[i];if(a.Mode!=b.Mode||!a.Input.Equals(b.Input)||a.Result!=b.Result||a.Native!=b.Native||a.Effective!=b.Effective||a.Observed!=b.Observed||a.Calls!=b.Calls)return false;}
            return true;
        }
        private void CompleteDecision(Scope root,Arguments after,bool completed)
        {
            int player=root.PlanningPlayer;if(player<1||player>8)return;
            long publishedId;
            lock(decisionGate)
            {
                var old=decisions[player];
                if(!completed) {decisions[player]=null;return;}
                if(old!=null&&!old.Plan.Equals(after)) {Emit("decision-state-ended","definition="+old.Id+",player="+player+",reason=consumed-plan-change");decisions[player]=null;}
                // Unchanged repeated checks retain their defining operation. A changed phase/target
                // without an access call is explicit; it never inherits an older permission result.
                long signature=17;foreach(var access in root.Accesses)
                {signature=unchecked(signature*397+access.Mode);signature=unchecked(signature*397+access.Result);signature=unchecked(signature*397+access.Input.GetHashCode());signature=unchecked(signature*397+access.Native);signature=unchecked(signature*397+access.Effective);signature=unchecked(signature*397+(access.Observed?1:0));}
                if(root.PrePlan.Equals(after)&&decisions[player]!=null&&(root.Accesses.Count==0||decisions[player].AccessSignature==signature&&SameAccesses(decisions[player].Accesses,root.Accesses)))return;
                if(root.Accesses.Count==0&&root.PrePlan.Equals(after))return;
                var value=new DecisionState {Id=++decisionId,Session=root.Session,Root=root.Id,Plan=after,AccessSignature=signature,Accesses=root.Accesses.ToArray()};decisions[player]=value;
                var rows=new StringBuilder();foreach(var access in root.Accesses)rows.Append(access).Append(';');
                Emit("decision-state","definition="+value.Id+",player="+player+",planningRoot="+root.Id+",entryPlan=["+root.PrePlan+"],consumedPlan=["+after+"],planColumns=lord/phase/targetPlayer/targetTile/x/y,accessColumns=op/mode/attackerActive/targetActive/attackerKeepTile/targetKeepTile/attackerKeepPcl/targetKeepPcl/return/regionObserved/native/effective/calls,accesses=["+rows+"],source=completed-caller-state,selectedAccessBranch=not-inferred");
                publishedId=value.Id;
            }
            lock(captureGate)foreach(var bridge in bridgeProgress)
            {
                bridge.Value.Decisions[player]=publishedId;bridge.Value.DecisionPhysical[player]=bridge.Value.PhysicalDefinition;bridge.Value.AccessObserved[player]=root.Accesses.Count!=0;
                Emit("decision-comparison","building="+bridge.Key+"/g"+bridge.Value.Global+",player="+player+",decisionState="+publishedId+",planningRoot="+root.Id+",physicalDefinition="+bridge.Value.PhysicalDefinition+",topologySettled="+!bridge.Value.Pending+",entryPhase="+root.PrePlan[1]+",consumedPhase="+after[1]+",targetPlayer="+after[2]+",targetTile="+after[3]+",accessObserved="+(root.Accesses.Count!=0)+",followingRoute=not-yet-observed,comparison=same-save-reload-not-proven");
            }
        }
        private struct Record
        {
            internal string Kind,Detail;
            internal long Seq,Session,Clock,Tick,Physical,Topology,Op,Parent,State,ScopeSession,Regions;
            internal int Thread;
            internal BridgeNativeDefinition.Site Site;
            internal Arguments Args;
            internal AttackStamp Stamp;
            internal Arguments PlanInput;
            internal long? Result;
            internal bool Completed;
            internal string Context;
        }
        private readonly struct RegionKey : IEquatable<RegionKey>
        {
            internal readonly int Player,From,To,Mode,Native,Effective;
            internal RegionKey(int player,int from,int to,int mode,int native,int effective) {Player=player;From=from;To=to;Mode=mode;Native=native;Effective=effective;}
            public bool Equals(RegionKey b) => Player==b.Player&&From==b.From&&To==b.To&&Mode==b.Mode&&Native==b.Native&&Effective==b.Effective;
            public override bool Equals(object b) => b is RegionKey other&&Equals(other);
            public override int GetHashCode() => unchecked((((((Player*397)^From)*397^To)*397^Mode)*397^Native)*397^Effective);
        }
        private struct RegionCount {internal long Count,FirstParent,LastParent;}
        private readonly object regionGate=new object();
        private readonly Dictionary<RegionKey,RegionCount> regionCounts=new Dictionary<RegionKey,RegionCount>();
        private readonly ManualLogSource log;
        private readonly ConcurrentQueue<Record> lines=new ConcurrentQueue<Record>();
        private readonly ConcurrentQueue<Record> background=new ConcurrentQueue<Record>();
        private long backgroundQueued, backgroundOverflow, commandPre, commandPost, backgroundCalls, detailedCommands;
        private readonly Dictionary<int,Arguments> plans=new Dictionary<int,Arguments>();
        private readonly Dictionary<int,Arguments> accessInputs=new Dictionary<int,Arguments>();
        private readonly Dictionary<int,long> accessResults=new Dictionary<int,long>();
        private readonly Dictionary<string,long> backgroundCounts=new Dictionary<string,long>();
        private readonly Dictionary<Arguments,long> eventCounts=new Dictionary<Arguments,long>();
        private readonly Dictionary<long,CommandIdentity> commands=new Dictionary<long,CommandIdentity>();
        private struct CommandIdentity {internal uint Global;internal Arguments State;}
        private readonly Dictionary<RegionKey,int> regionIds=new Dictionary<RegionKey,int>();
        private long lastCaptures,lastBuilds,lastCoalesced,lastOutputBytes,lastCaptureTicks,lastDrainTicks,lastBackgroundCalls,lastCostClock;
        private long lastIndexTicks;
        private readonly long[] lastPlayerPlan=new long[9],lastPlayerPlanTopology=new long[9],lastPlayerPlanPhysical=new long[9];
        private long freshPlans,followingCommands,lastPlanPhysical=-1,lastPlanTopology=-1;
        private readonly object captureGate=new object(),attackGate=new object(),counterGate=new object();
        private readonly Dictionary<int,AttackStamp> attacks=new Dictionary<int,AttackStamp>();
        internal sealed class NumericImage : IEquatable<NumericImage>
        {
            internal long[] Data;
            internal int Count;
            internal long ObservedAt;
            internal string Link;
            internal NumericImage(int capacity) {Data=new long[capacity];}
            internal void Add(long value) {if(Count==Data.Length)Array.Resize(ref Data,Math.Max(16,Count*2));Data[Count++]=value;}
            internal NumericImage Copy() {var copy=new NumericImage(Count) {Count=Count,Link=Link,ObservedAt=ObservedAt};Array.Copy(Data,copy.Data,Count);return copy;}
            public bool Equals(NumericImage other)
            {if(other==null||Count!=other.Count||Link!=other.Link)return false;for(int i=0;i<Count;i++)if(Data[i]!=other.Data[i])return false;return true;}
            public override bool Equals(object other) => other is NumericImage image&&Equals(image);
            public override int GetHashCode() {int hash=Link?.GetHashCode()??0;for(int i=0;i<Count;i++)hash=unchecked(hash*397^Data[i].GetHashCode());return hash;}
        }
        private readonly Dictionary<NumericImage,long> physicalDefinitions=new Dictionary<NumericImage,long>();
        private readonly Dictionary<NumericImage,long> planningDefinitions=new Dictionary<NumericImage,long>();
        private readonly Dictionary<NumericImage,long> numericStates=new Dictionary<NumericImage,long>();
        private readonly NumericImage physicalScratch=new NumericImage(350),planningScratch=new NumericImage(260),stateScratch=new NumericImage(32);
        private long readTicks,compareTicks,formatTicks,lastReadTicks,lastCompareTicks,lastFormatTicks;
        private long numericReads,definitionFormats,lastNumericReads,lastDefinitionFormats,tableReferences,lastTableReferences;
        private long tableReads,lastTableReads;
        private int unscopedGroupSamples;
        private readonly Dictionary<NumericImage,long> candidateDefinitions=new Dictionary<NumericImage,long>();
        private readonly NumericImage candidateScratch=new NumericImage(4010);
        private readonly Dictionary<TableReference,long> tableRepeats=new Dictionary<TableReference,long>();
        private readonly struct TableReference : IEquatable<TableReference>
        {
            internal readonly long Parent,Definition,Plan;
            internal readonly int Rva,Slot;
            internal readonly string Phase;
            internal TableReference(long parent,TableImage image,string phase) {Parent=parent;Definition=image.Definition;Plan=image.Plan;Rva=image.Rva;Slot=image.Slot;Phase=phase;}
            public bool Equals(TableReference b) => Parent==b.Parent&&Definition==b.Definition&&Plan==b.Plan&&Rva==b.Rva&&Slot==b.Slot&&Phase==b.Phase;
            public override bool Equals(object b) => b is TableReference other&&Equals(other);
            public override int GetHashCode() => unchecked(Parent.GetHashCode()*397^Definition.GetHashCode()*31^Plan.GetHashCode()^Rva^Slot^(Phase?.GetHashCode()??0));
        }
        private readonly Dictionary<int,BridgeProgress> bridgeProgress=new Dictionary<int,BridgeProgress>();
        private sealed class BridgeProgress
        {
            internal uint Global;
            internal long PhysicalDefinition, Changes;
            internal bool Pending;
            internal readonly long[] Plans=new long[9], PlanDefinitions=new long[9];
            internal readonly long[] StoredRoutePlans=new long[9],ExecutedRoutePlans=new long[9],StoredRouteRoots=new long[9],ExecutedRouteRoots=new long[9],StoredRouteDecisions=new long[9],ExecutedRouteDecisions=new long[9];
            internal readonly long[] Decisions=new long[9],DecisionPhysical=new long[9],StoredRoutePhysical=new long[9];
            internal readonly bool[] AccessObserved=new bool[9];
            internal readonly long[] SelectedPlans=new long[9],AssignedPlans=new long[9],CommandPlans=new long[9];
            internal readonly HashSet<int> Tiles=new HashSet<int>();
        }
        internal sealed class TableImage
        {
            internal int Count,Available,Rva,Slot;
            internal int[] Rows;
            internal long Definition,Plan;
        }
        private readonly Dictionary<long,TableImage> tableImages=new Dictionary<long,TableImage>();
        internal sealed class SelectionEvidence
        {
            internal long Op,Definition,Plan;
            internal int Unit,Task,Approach,Player,X,Y,Row,Table;
            internal uint Global;
            internal bool Unique;
            internal long Movement;
        }
        internal readonly BridgeRouteTrace Routes;
        internal readonly BridgeVirtualShadow VirtualShadow;
        private BridgeRouteTrace.Bridge[] routeGeometry=Array.Empty<BridgeRouteTrace.Bridge>();
        private long routeGeometryBuild=-1;
        private readonly Dictionary<string,long> textDefinitions=new Dictionary<string,long>(StringComparer.Ordinal);
        private readonly StringBuilder commandRows=new StringBuilder();
        private int commandRowCount;
        private readonly struct RetainedCommand {internal readonly CommandData Data;internal readonly long Seq;internal RetainedCommand(CommandData data,long seq){Data=data;Seq=seq;}}
        private readonly Dictionary<long,RetainedCommand> commandPreRows=new Dictionary<long,RetainedCommand>();
        private static bool SameCommandInputs(CommandData a,CommandData b)=>a.Parent==b.Parent&&a.Kind==b.Kind&&a.Id==b.Id&&a.Global==b.Global&&a.Tribe==b.Tribe&&a.Player==b.Player&&a.Command==b.Command&&a.X==b.X&&a.Y==b.Y&&a.StartX==b.StartX&&a.StartY==b.StartY&&a.Source==b.Source&&a.Target==b.Target&&a.Type==b.Type&&a.Extra==b.Extra&&a.Promoted==b.Promoted&&a.Near==b.Near&&a.Attribution==b.Attribution;

        private readonly StringBuilder searchRows=new StringBuilder();private int searchRowCount;
        private readonly Dictionary<Arguments,long> commandIdentities=new Dictionary<Arguments,long>();
        private readonly Dictionary<CommandContext,long> commandContexts=new Dictionary<CommandContext,long>();
        private long commandContextId;
        private readonly struct CommandContext : IEquatable<CommandContext>
        {
            internal readonly long Parent,Plan,Physical,Topology,State;
            internal CommandContext(long parent,long plan,long physical,long topology,long state)
            {Parent=parent;Plan=plan;Physical=physical;Topology=topology;State=state;}
            public bool Equals(CommandContext b) => Parent==b.Parent&&Plan==b.Plan&&Physical==b.Physical&&Topology==b.Topology&&State==b.State;
            public override bool Equals(object b) => b is CommandContext value&&Equals(value);
            public override int GetHashCode() => unchecked((((Parent.GetHashCode()*397^Plan.GetHashCode())*397^Physical.GetHashCode())*397^Topology.GetHashCode())*397^State.GetHashCode());
            public override string ToString() => Parent+"/"+Plan+"/"+Physical+"/"+Topology+"/"+State;
        }
        private readonly object textGate=new object();
        private long textId,groupDefinition;
        private readonly Dictionary<NumericImage,long> groupDefinitions=new Dictionary<NumericImage,long>();
        private readonly NumericImage groupScratch=new NumericImage(2000);
        private readonly struct EndedSession
        {
            internal readonly long Session;
            internal readonly bool CaptureComplete;
            internal EndedSession(long session,bool complete) {Session=session;CaptureComplete=complete;}
        }
        private readonly Queue<EndedSession> endedSessions=new Queue<EndedSession>();
        private long deliveryErrors;
        private long fragmentId;
        private readonly BridgeBuildingIndex buildingIndex=new BridgeBuildingIndex();
        private readonly Func<int,AttackStamp> testStamp;
        private readonly Func<long> testCapture;
        private readonly Func<int,int> testRead;
        private readonly Func<int,IntPtr> testUnit;
        private long stateId,tableId,sequence,session,entered,exited,active,failures,overflow,queued,topology,physical;
        private long incompleteCalls;
        private long coalesced,captures,captureTicks,drainTicks,outputBytes,outputRecords,pooledScopes,invalidIds;
        private readonly long[] siteCalls=new long[64];
        private long tick=-1;
        private IntPtr native;
        private int nativeLength,installed;
        [ThreadStatic] private static Scope current;
        [ThreadStatic] private static Scope free;
        internal BridgeDecisionTrace(ManualLogSource log,Func<int,AttackStamp> testStamp=null,Func<long> testCapture=null,Func<int,int> testRead=null,Func<int,IntPtr> testUnit=null)
        {this.log=log;VirtualShadow=testCapture==null?new BridgeVirtualShadow(Observe,Read,ArtifactObservation,focusedComparisons:true):null;this.testStamp=testStamp;this.testCapture=testCapture;this.testRead=testRead;this.testUnit=testUnit;
            Routes=new BridgeRouteTrace((kind,detail)=>CriticalObservation(kind,detail),GetRouteGeometry,LiveState,evidence:RouteEvidence,unboundChange:UnboundRouteChange);}
        private BridgePlanningCapture planningCapture;
        internal void SetNative(IntPtr value,int length)
        {
            native=value;nativeLength=length;
            // Counter/route fixtures intentionally use non-dereferenceable dummy pointers.
            // Full capture is tested separately with the bounded private native image.
            if(testCapture!=null){planningCapture=null;return;}
            planningCapture=new BridgePlanningCapture(length,(r,n)=>{if(r<0||n<0||(long)r+n>nativeLength)throw new InvalidOperationException("Planning copy outside module");var bytes=new byte[n];Marshal.Copy(IntPtr.Add(native,r),bytes,0,n);return bytes;},()=>planningMapIdentity, bundle=>{if(VirtualShadow==null||!VirtualShadow.PlanningCompleted(bundle))planningCapture?.PublicationRejected(bundle.Op,"no-matching-artifact-input");},Observe,requireMilitaryRoot:true,prepare:bundle=>VirtualShadow!=null&&VirtualShadow.PreparePlanning(bundle,buildingIndex));
        }
        private string nativeUnavailable="initialization-not-completed";
        private long sessionNativeCalls,sessionNativeExits;
        private object planningMapIdentity;
        internal void MarkInstalled(int count) {installed=count;nativeUnavailable="none";}
        internal void MarkNativeUnavailable(string reason) {nativeUnavailable=reason.Replace(',',';').Replace('\r',' ').Replace('\n',' ');}
        internal string NativeReadiness => "installedEntries="+installed+",expectedEntries="+BridgeNativeDefinition.OwnedCount+",ownNativeReady="+(installed==BridgeNativeDefinition.OwnedCount)+",nativeReady="+(installed==BridgeNativeDefinition.OwnedCount&&APIShared.EnemyBridgeDiagnosticBridge.TopologyAvailable)+",nativeCallsObserved="+(Interlocked.Read(ref sessionNativeCalls)!=0)+",shadowStatus="+(installed==BridgeNativeDefinition.OwnedCount&&APIShared.EnemyBridgeDiagnosticBridge.TopologyAvailable?"waiting-for-observed-rebuild-and-fresh-decision":"unavailable")+",topologyOwner=APIShared,topologyAvailable="+APIShared.EnemyBridgeDiagnosticBridge.TopologyAvailable+",ladderCoverage=effective-consumer-only,topologySuppressed="+topologySuppressed+",reason=["+nativeUnavailable+"]";
        private long topologySuppressed;
        internal object BeginTopology(IntPtr manager,int force,bool originalWillRun,bool knownPathingContext=true)
        {
            if (!originalWillRun) {Interlocked.Increment(ref topologySuppressed);return null;}
            if (!knownPathingContext) {Failure(new InvalidOperationException("shared-topology-context-unresolved"));return null;}
            if (native==IntPtr.Zero) return null;
            return Enter(BridgeNativeDefinition.TopologySite,manager,force);
        }
        internal void EndTopology(object token,bool completed,bool originalCalled,int? nativeResult,int effectiveResult)
        {
            if (token is Scope scope) Exit(scope,completed&&originalCalled,nativeResult.HasValue?(long?)nativeResult.Value:null,IntPtr.Zero);
        }
        internal bool InsideNative => current!=null;
        private Scope CurrentScope => current!=null&&current.Session==Volatile.Read(ref session)?current:null;
        private long CurrentId => CurrentScope?.Id??0;
        private static bool SuitablePlanningCapture(Scope scope){for(var value=scope.Parent;value!=null&&value.Session==scope.Session;value=value.Parent)if(value.Site.Rva==0x3C2E0)return value.PrePlan[1]==4;return false;}
        private static long PlanningRootId(Scope scope) {for(var value=scope.Parent;value!=null&&value.Session==scope.Session;value=value.Parent)if(value.Site.Rva==0x3C2E0)return value.PlanningPlayer==scope.PlanningPlayer?value.Id:0;return 0;}
        private static long ParentId(Scope scope) => scope.Parent!=null&&scope.Parent.Session==scope.Session?scope.Parent.Id:0;
        internal bool DetailedNative => CurrentScope!=null&&CurrentScope.Detailed&&CurrentScope.Site.Rva!=0xE49D0;
        private bool FreshDecisionContext
        {
            get {for(Scope scope=CurrentScope;scope!=null&&scope.Session==session;scope=scope.Parent)if(scope.CandidateBuilds>0)return true;return false;}
        }
        internal bool ShouldDetailCommand(bool group,bool near,bool changed) => DetailedNative&&(changed||FreshDecisionContext)||near&&changed||group&&changed&&Interlocked.Increment(ref unscopedGroupSamples)<=8;
        internal static Arguments CommandCountData(int kind,int player,int command,long result,bool pre,bool resolved) =>
            new Arguments(kind,player,command,unchecked((int)result),unchecked((int)(result>>32)),(pre?0:1)|(resolved?2:0));
        internal void Tick(int value) => Interlocked.Exchange(ref tick,value);
        internal void InvalidateBuildings() {buildingIndex.Invalidate();VirtualShadow?.Invalidate();}
        internal void Begin(Shared.GameplaySessionStartedContext context)
        {
            StartSession(context.SessionId);
            Emit("session-start","mode="+context.Mode.ToDiagnosticString()+",save="+context.IsLoadedSave+",editor="+context.IsEditor+
                ",installedEntries="+installed+",coalescing=exact-repeat,routeAttribution=unproven");
        }
        internal void StartSession(long value)
        {
            Routes.Flush();FlushCommandRows();FlushSearchRows();FlushTableReferences();
            lock(attackGate) attacks.Clear();
            lock(regionGate) {regionCounts.Clear();regionIds.Clear();backgroundCounts.Clear();}
            lock(attackGate) {plans.Clear();commands.Clear();accessInputs.Clear();accessResults.Clear();}
            lock(textGate){commandContexts.Clear();commandIdentities.Clear();commandPreRows.Clear();}
            lock(regionGate) eventCounts.Clear();
            lock(captureGate) {tableImages.Clear();bridgeProgress.Clear();buildingIndex.Invalidate();}
            planningMapIdentity=new object();Interlocked.Exchange(ref session,value);
            Interlocked.Exchange(ref sessionNativeCalls,0);Interlocked.Exchange(ref sessionNativeExits,0);
            VirtualShadow?.Begin(value);planningCapture?.Begin(value);Routes.Begin(value);routeGeometryBuild=-1;lock(decisionGate)Array.Clear(decisions,0,decisions.Length);
            Array.Clear(lastPlayerPlan,0,9);Array.Clear(lastPlayerPlanTopology,0,9);Array.Clear(lastPlayerPlanPhysical,0,9);
            freshPlans=followingCommands=0;lastPlanPhysical=lastPlanTopology=-1;
            // Actual cell/index reads wait for the next simulation callback, never run on the render thread.
        }
        internal void End()
        {
            planningMapIdentity=null;planningCapture?.End();VirtualShadow?.End();Routes.End();FlushRegions();
            var currentDecisions=new long[9];lock(decisionGate)for(int player=1;player<=8;player++)currentDecisions[player]=decisions[player]?.Id??0;
            lock(captureGate)foreach(var pair in bridgeProgress)for(int player=1;player<=8;player++)
            {
                var bridge=pair.Value;if(bridge.Decisions[player]==0&&bridge.StoredRouteRoots[player]==0)continue;
                bool matching=bridge.AccessObserved[player]&&bridge.Decisions[player]!=0&&currentDecisions[player]==bridge.Decisions[player]&&bridge.Decisions[player]==bridge.StoredRouteDecisions[player]&&bridge.DecisionPhysical[player]==bridge.PhysicalDefinition&&bridge.StoredRoutePhysical[player]==bridge.PhysicalDefinition&&!bridge.Pending;
                Emit("comparison-coverage","building="+pair.Key+"/g"+bridge.Global+",player="+player+",physicalDefinition="+bridge.PhysicalDefinition+",topologySettled="+!bridge.Pending+",decisionState="+bridge.Decisions[player]+",accessObserved="+bridge.AccessObserved[player]+",storedRouteDecision="+bridge.StoredRouteDecisions[player]+",storedRoutePhysical="+bridge.StoredRoutePhysical[player]+",planningRoot="+bridge.StoredRouteRoots[player]+",samePhysicalDecisionAndRoute="+matching+",missing="+(matching?"controlled-same-save-counterrun":"fresh-matching-access-and-route-or-settled-topology")+",execution=separate-evidence,comparison=same-save-reload-not-proven");
            }
            // One bounded synchronous record survives an immediate process exit.
            // Pending observations continue through the permanent render publisher.
            Safe(() => WriteRecord(StampRecord(new Record {Kind="session-end",Detail=Summary()+",captureComplete="+(active==0&&failures==0&&incompleteCalls==0&&overflow==0&&backgroundOverflow==0)+",deliveryComplete="+(queued==0)+",criticalPending="+(queued-backgroundQueued)+",backgroundPending="+backgroundQueued})));
            lock(endedSessions)endedSessions.Enqueue(new EndedSession(Volatile.Read(ref session),active==0&&failures==0&&incompleteCalls==0&&overflow==0&&backgroundOverflow==0));
            Interlocked.Exchange(ref session,0);
        }
        internal long Entered => Interlocked.Read(ref entered);
        internal long Exited => Interlocked.Read(ref exited);
        internal long Captures => Interlocked.Read(ref captures);
        internal long Pending => Interlocked.Read(ref queued);
        internal long OutputRecords => Interlocked.Read(ref outputRecords);
        internal long ScopeAllocations => Interlocked.Read(ref pooledScopes);
        internal long Coalesced => Interlocked.Read(ref coalesced);
        internal long Failures => Interlocked.Read(ref failures);
        internal string Summary()
        {
            long capturedEntered,capturedExited,capturedActive;
            lock(counterGate) {capturedEntered=entered;capturedExited=exited;capturedActive=active;}
            var sites=new StringBuilder();
            foreach(var site in BridgeNativeDefinition.Sites) sites.Append(site.Name).Append(':').Append(Interlocked.Read(ref siteCalls[site.Index])).Append(';');
            return "entered="+capturedEntered+",exited="+capturedExited+",active="+capturedActive+",balanced="+(capturedEntered==capturedExited+capturedActive)+
                ",incompleteCalls="+incompleteCalls+",captureFailures="+failures+",overflow="+overflow+",backgroundOverflow="+backgroundOverflow+",traceComplete="+(failures==0&&incompleteCalls==0&&overflow==0&&backgroundOverflow==0)+",deliveryPending="+(queued!=0)+",installedEntries="+installed+",expectedEntries="+BridgeNativeDefinition.OwnedCount+",sessionNativeCalls="+Interlocked.Read(ref sessionNativeCalls)+",sessionNativeExits="+Interlocked.Read(ref sessionNativeExits)+",nativeCoverageComplete="+(installed==BridgeNativeDefinition.OwnedCount&&APIShared.EnemyBridgeDiagnosticBridge.TopologyAvailable&&Interlocked.Read(ref sessionNativeCalls)>0&&Interlocked.Read(ref sessionNativeCalls)==Interlocked.Read(ref sessionNativeExits)&&capturedEntered==capturedExited&&capturedActive==0&&failures==0&&incompleteCalls==0)+",nativeUnavailable=["+nativeUnavailable+"]"+
                ","+(planningCapture?.Status??"planningCapture=unavailable")+",physicalGeneration="+physical+",topologyGeneration="+topology+",fullCaptures="+captures+",indexBuilds="+buildingIndex.Builds+",indexBuildMs="+(buildingIndex.BuildTicks*1000.0/Stopwatch.Frequency).ToString("F3")+
                ",coalesced="+coalesced+",invalidIds="+invalidIds+",pooledScopesCreated="+pooledScopes+",queue="+queued+
                ",commandPre="+commandPre+",commandPost="+commandPost+",detailedCommands="+detailedCommands+",backgroundCalls="+backgroundCalls+
                ",deliveryErrors="+deliveryErrors+",outputBytes="+outputBytes+",outputRecords="+outputRecords+",captureMs="+(captureTicks*1000.0/Stopwatch.Frequency).ToString("F3")+
                ",drainMs="+(drainTicks*1000.0/Stopwatch.Frequency).ToString("F3")+",siteCalls=["+sites+"]";
        }
        internal Scope Enter(BridgeNativeDefinition.Site site,IntPtr pointer,int a=0,int b=0,int c=0,int d=0,int e=0,int f=0)
        {
            long run=Volatile.Read(ref session);if(run==0) return null;
            Scope scope=free;
            if(scope==null) {scope=new Scope();Interlocked.Increment(ref pooledScopes);} else free=scope.Next;
            scope.Next=null;scope.Parent=current;scope.Site=site;scope.Args=new Arguments(a,b,c,d,e,f);
            scope.Id=Interlocked.Increment(ref sequence);scope.Session=run;scope.Regions=0;scope.Detailed=false;scope.PreStamp=default;
            scope.PrePlan=default;scope.Commands=scope.CandidateBuilds=0;
            scope.AccessValid=false;scope.AccessInput=default;scope.AccessNative=scope.AccessEffective=0;scope.AccessRegionObserved=false;scope.Accesses.Clear();scope.SelectedUnit=0;scope.SelectedGlobal=0;
            scope.Rebuilt=false;scope.SelectionTables.Clear();scope.Selections.Clear();scope.LadderBefore=null;scope.LadderCommands.Clear();
            scope.RegionSamples.Clear();
            scope.PlanningPlayer=PlanningSite(site.Rva)?a:scope.Parent!=null&&scope.Parent.Session==run?scope.Parent.PlanningPlayer:0;
            current=scope;lock(counterGate) {entered++;active++;} Interlocked.Increment(ref siteCalls[site.Index]);Interlocked.Increment(ref sessionNativeCalls);
            try
            {
                planningCapture?.Observe(site.Rva,false,run,scope.Id,ParentId(scope),scope.PlanningPlayer,a,b,c,d,true,PlanningRootId(scope),SuitablePlanningCapture(scope));
                if(site.Rva==0x2D250)VirtualShadow?.ExpectPlanningFamily(planningCapture?.ActiveFamily??0);
                if(site.Rva==0x11A980)
                {
                    scope.PreStamp=ReadStamp(a);
                    lock(attackGate) {scope.Detailed=!attacks.TryGetValue(a,out AttackStamp previous)||!previous.Equals(scope.PreStamp);attacks[a]=scope.PreStamp;}
                }
                else if(site.Rva==0x3C2E0)
                {
                    scope.PrePlan=PlanStamp(a);DecisionFor(a,scope.PrePlan);
                    lock(attackGate) {scope.Detailed=!plans.TryGetValue(a,out Arguments old)||!old.Equals(scope.PrePlan);plans[a]=scope.PrePlan;}
                }
                else if(AccessSite(site.Rva))
                {
                    bool military=scope.Parent!=null&&scope.Parent.Session==run&&scope.Parent.PlanningPlayer!=0;
                    if(military)
                    {
                        scope.AccessInput=ReadAccessInput(pointer,a,b);scope.AccessValid=true;
                        int key=AccessKey(scope);
                        lock(attackGate) {scope.Detailed=scope.Parent.Detailed||!accessInputs.TryGetValue(key,out Arguments old)||!old.Equals(scope.AccessInput);accessInputs[key]=scope.AccessInput;}
                    }
                }
                else scope.Detailed=site.Rva!=0xE49D0||TopologyWillRebuild(a,Read(0x60AD6CC),Read(0x37ED4CC));
                if(site.Name.StartsWith("select-")&&testCapture==null) lock(captureGate)CaptureSelectionInput(scope);
                if(site.Rva==0x122B40&&(b==0x3F3||b==0x3F4)&&testCapture==null)lock(captureGate)
                {
                    var api=GameTribeManagerAPI.Instance;
                    if(api.IsValidId(a)&&api.TryGetTribeById(a,out GameTribe* tribe)&&tribe!=null&&tribe->r_PlayerIdOwner>=1&&tribe->r_PlayerIdOwner<=8)
                    {
                        int owner=tribe->r_PlayerIdOwner;Table(0x2EAAF90,owner,1000,8,"ladder-consumer-pre");
                        scope.LadderBefore=tableImages[((long)0x2EAAF90<<32)|(uint)owner];
                    }
                    else Emit("task-followup-gap","consumer="+scope.Id+",reason=unresolved-ladder-consumer-owner");
                }
                if(scope.Detailed) Entry(scope,true);
            }
            catch(Exception error) {Failure(error);}
            return scope;
        }
        internal static bool TopologyWillRebuild(int force,int dirty,int countdown) => force!=0||(dirty!=0&&unchecked(countdown-1)<=0);
        private static bool AccessSite(int rva) => rva==0xCF360||rva==0xCF400;
        private static int AccessKey(Scope scope) => (scope.Site.Rva==0xCF400?81:0)+scope.Args[0]*9+scope.Args[1];
        private static bool PlanningSite(int rva) => rva==0x3C2E0||rva==0x2D250||rva==0x2C480||rva==0x2C5A0||rva==0x3BD50;
        private Arguments PlanStamp(int player)
        {
            if(testStamp!=null&&testRead==null) return default;
            if(player<1||player>8) return default;
            int delta=player*0x583C;
            return new Arguments(Read(0x379D0D0+delta),Read(0x379D974+delta),Read(0x379D9A8+delta),Read(0x379D968+delta),Read(0x379D970+delta),Read(0x379D96C+delta));
        }
        private AttackStamp ReadStamp(int tribeId)
        {
            if(testStamp!=null) return testStamp(tribeId);
            var api=GameTribeManagerAPI.Instance;
            if(!api.IsValidId(tribeId)||!api.TryGetTribeById(tribeId,out GameTribe* tribe)||tribe==null)
            {Interlocked.Increment(ref invalidIds);return default;}
            int record=checked(0x7CC6720+tribeId*0x688);
            return new AttackStamp(tribe->r_GlobalId,tribe->r_PlayerIdOwner,tribe->r_LeaderUnitId,
                (short)(Read(record+0x50)&65535),(short)(Read(record+0x634)&65535),(short)(Read(record+0x63C)&65535),unchecked((uint)Read(record+0x640)));
        }
        private void Entry(Scope scope,bool atEntry)
        {
            if(scope.Parent!=null&&scope.Parent.Session==scope.Session&&!scope.Parent.Detailed) {scope.Parent.Detailed=true;Entry(scope.Parent,false);}
            scope.Detailed=true;
            Enqueue(new Record {Kind="native-enter",Site=scope.Site,Op=scope.Id,Parent=ParentId(scope),Args=scope.Args,
                ScopeSession=scope.Session,Stamp=scope.PreStamp,PlanInput=scope.PrePlan,State=LiveState(),Context="capturePhase="+(atEntry?"entry":"promoted-later")+","+(testStamp!=null?"fixture=true":scope.Site.Rva==0x11A980?"inputStamp=entry":Context(scope.Site,scope.Args))});
            if(atEntry) lock(captureGate) CaptureTables(scope.Site,scope.Args,"pre",IntPtr.Zero);
        }
        internal void Exit(Scope scope,bool completed,long? result,IntPtr pointer)
        {
            if(scope==null) return;
            try
            {
                if(scope.Site.Rva==0x3C2E0){var consumed=PlanStamp(scope.Args[0]);planningCapture?.Outcome(scope.Session,scope.Id,new[]{scope.PrePlan[0],scope.PrePlan[1],scope.PrePlan[2],scope.PrePlan[3],scope.PrePlan[4],scope.PrePlan[5]},new[]{consumed[0],consumed[1],consumed[2],consumed[3],consumed[4],consumed[5]});}
                planningCapture?.Observe(scope.Site.Rva,true,scope.Session,scope.Id,ParentId(scope),scope.PlanningPlayer,scope.Args[0],scope.Args[1],scope.Args[2],scope.Args[3],completed,PlanningRootId(scope));
                if(scope.Site.Rva==0x2D250)VirtualShadow?.ExpectPlanningFamily(0);
                if(!completed)Interlocked.Increment(ref incompleteCalls);
                if(scope.Session!=Volatile.Read(ref session)) {Emit("native-session-boundary","op="+scope.Id+",preSession="+scope.Session+",postSession="+session+",nativeCalls=1,completed="+completed);return;}
                if(completed&&(scope.Site.Rva==0x64460||scope.Site.Rva==0x645C0)) {Interlocked.Increment(ref physical);VirtualShadow?.Invalidate();Routes.ForceReobserve();lock(captureGate) {if(bridgeProgress.TryGetValue(scope.Args[0],out BridgeProgress bridge)) {bridge.Pending=true;bridge.Changes++;}}Emit("comparison-marker","stage=physical-call-completed,building="+scope.Args[0]+",action="+scope.Site.Name+",settledTopology=false");}
                if(scope.Site.Rva==0xE49D0&&completed&&result==1) {Interlocked.Increment(ref topology);scope.Rebuilt=true;if(testCapture==null)lock(captureGate)VirtualShadow?.Rebuilt(session,topology,buildingIndex);}
                if(scope.Site.Rva==0x3C2E0)
                {
                    Arguments after=PlanStamp(scope.Args[0]);
                    if(!after.Equals(scope.PrePlan)&&!scope.Detailed) Entry(scope,false);
                    lock(attackGate) plans[scope.Args[0]]=after;
                    CompleteDecision(scope,after,completed);
                    if(completed&&scope.Accesses.Count!=0)VirtualShadow?.Defer("selected-target-hypothesis-covered-by-strategic-keep");
                }
                if(scope.Site.Rva==0x11A980)
                {
                    AttackStamp after=ReadStamp(scope.Args[0]);
                    if(!after.Equals(scope.PreStamp)&&!scope.Detailed) Entry(scope,false);
                    lock(attackGate) attacks[scope.Args[0]]=after;
                }
                if(scope.AccessValid&&completed&&result.HasValue)
                {
                    CountEvent(new Arguments(5,scope.Args[0],scope.Args[1],scope.Site.Rva==0xCF400?1:0,unchecked((int)result.Value),unchecked((int)(result.Value>>32))));
                    Scope root=scope.Parent;while(root!=null&&root.Session==scope.Session&&root.Site.Rva!=0x3C2E0)root=root.Parent;
                    if(root!=null&&root.Session==scope.Session){root.Accesses.Add(new AccessObservation(scope,result.Value));planningCapture?.Access(scope.Session,root.Id,scope.Id,scope.Site.Rva==0xCF400?1:0,scope.AccessNative,scope.AccessEffective,scope.AccessRegionObserved,result.Value,new[]{scope.AccessInput[0],scope.AccessInput[1],scope.AccessInput[2],scope.AccessInput[3],scope.AccessInput[4],scope.AccessInput[5]});}
                    int key=AccessKey(scope);bool changed;
                    lock(attackGate) {changed=!accessResults.TryGetValue(key,out long old)||old!=result.Value;accessResults[key]=result.Value;}
                    if(changed||scope.Parent!=null&&scope.Parent.Site.Rva==0x2D250)
                        VirtualShadow?.Compare("keep-access",scope.Id,ParentId(scope),scope.Args[0],scope.AccessInput[2],scope.AccessInput[3],scope.Site.Rva==0xCF400?1:0,unchecked((int)result.Value));
                    else VirtualShadow?.Defer("unchanged-keep-outside-fresh-planning");
                    if(changed&&!scope.Detailed)Entry(scope,false);
                }
                if(!scope.Detailed&&(!completed||(scope.Site.Rva==0xE49D0&&result==1))) Entry(scope,false);
                if(scope.Detailed)
                {
                    string output="";
                    if(completed&&result!=0&&scope.Site.Name.StartsWith("select-")) output=",selectedOutput="+ReadOutput(pointer,0);
                    if(completed&&result!=0&&scope.Site.Rva==0xE7F60) output=",selectedAccess="+ReadOutput(pointer,0x18,2);
                    Enqueue(new Record {Kind="native-exit",Site=scope.Site,Op=scope.Id,Parent=ParentId(scope),Args=scope.Args,
                        Result=result,Completed=completed,Regions=scope.Regions,ScopeSession=scope.Session,PlanInput=scope.PrePlan,State=LiveState(),Context=(testStamp!=null?"fixture=true":Context(scope.Site,scope.Args))+output});
                    lock(captureGate) CaptureTables(scope.Site,scope.Args,"post",pointer);
                    if(scope.AccessValid) Emit("keep-access-decision","op="+scope.Id+",parent="+ParentId(scope)+",attacker="+scope.Args[0]+",targetPlayer="+scope.Args[1]+",mode="+(scope.Site.Rva==0xCF400?1:0)+",inputs=["+scope.AccessInput+"],columns=attackerActiveRaw/targetActiveRaw/attackerKeepTile/targetKeepTile/attackerKeepPcl/targetKeepPcl,branch="+AccessBranch(scope.AccessInput)+",branchEvidence=audited-entry-data,regionCallsObserved="+scope.Regions+",completed="+completed+",effectiveReturn="+result+",followingPhaseRaw="+PlanStamp(scope.Args[0])[1]+",phaseTiming=before-caller-consumes-return");
                    if(scope.Site.Name.StartsWith("select-")&&completed&&result!=0&&testCapture==null) lock(captureGate)CompleteSelection(scope,pointer);
                    if(scope.Site.Rva==0x122B40&&completed&&testCapture==null) lock(captureGate){CompleteLadderAssignments(scope);CompleteTaskAssignments(scope);}
                    if(scope.Site.Rva==0xE49D0&&completed&&result==1) Emit("comparison-marker","stage=topology-rebuilt,observedReturn=1,op="+scope.Id);
                    if(scope.Site.Rva==0x2C480&&completed)
                    {
                        scope.CandidateBuilds++;Interlocked.Increment(ref freshPlans);
                        if(scope.PlanningPlayer>=1&&scope.PlanningPlayer<=8) {lastPlayerPlan[scope.PlanningPlayer]=scope.Id;lastPlayerPlanTopology[scope.PlanningPlayer]=topology;lastPlayerPlanPhysical[scope.PlanningPlayer]=physical;}
                        lastPlanPhysical=physical;lastPlanTopology=topology;
                        Emit("comparison-marker","stage=fresh-candidates-completed,player="+scope.PlanningPlayer+",op="+scope.Id+",followCommandObserved="+(scope.Commands>0));
                        lock(captureGate) MarkBridgePlanning(scope);
                    }
                    if(scope.Site.Rva==0x3C2E0) Emit("planning-outcome","op="+scope.Id+",player="+scope.PlanningPlayer+",entryPhase="+scope.PrePlan[1]+",exitPhase="+PlanStamp(scope.Args[0])[1]+",phaseTiming=after-caller-consumed-access-return"+",freshCandidateBuilds="+scope.CandidateBuilds+",commands="+scope.Commands+",completed="+completed+",coverage="+(overflow==0&&backgroundOverflow==0&&failures==0?"observed-chain":"incomplete")+",commandAbsence=within-this-call-only");
                }
                else Interlocked.Increment(ref coalesced);
            }
            catch(Exception error) {Failure(error);}
            finally {if(scope.Session==Volatile.Read(ref session))Interlocked.Increment(ref sessionNativeExits);current=scope.Parent;if(current!=null&&current.Session==scope.Session) {current.Commands+=scope.Commands;current.CandidateBuilds+=scope.CandidateBuilds;}scope.Parent=null;scope.Next=free;free=scope;lock(counterGate) {exited++;active--;}}
        }
        internal long NewOperation() => Interlocked.Increment(ref sequence);
        internal void CompareGroupShadow(long op,long parent,int player,int x,int y,int targetX,int targetY,int tribe,uint global)
        {
            VirtualShadow?.CompareXY("group-formation",op,parent,player,x,y,targetX,targetY);
            Scope root=CurrentScope;while(root!=null&&root.Site.Rva!=0x3C2E0)root=root.Parent;
            bool linked=root!=null&&root.PlanningPlayer==player;
            long decision=linked?DecisionFor(player,root.PrePlan):0;
            VirtualShadow?.GroupContext(op,tribe,global,linked?root.Id:0,decision,linked?root.PrePlan[1]:-1,DecisionRootFor(player,decision));
        }
        internal bool BindRoute(int unit,uint global,int player,int tribe,long operation,long parentEvent,bool changed)
        {
            Scope consumer=CurrentScope,root=consumer;
            while(root!=null&&root.Site.Rva!=0x3C2E0)root=root.Parent;
            if(root==null&&(!Routes.IsTracked(unit)||!changed))return false;
            int attacker=root!=null&&root.PlanningPlayer>=1&&root.PlanningPlayer<=8?root.PlanningPlayer:player;
            long plan=attacker>=1&&attacker<=8?lastPlayerPlan[attacker]:0;
            if(changed||FreshDecisionContext||!Routes.IsTracked(unit))
                return Routes.Bind(unit,global,attacker,tribe,operation,parentEvent,consumer?.Id??0,root?.Id??0,plan,root==null?-1:root.PrePlan[1],player,root==null?0:DecisionFor(attacker,root.PrePlan));
            return false;
        }
        private BridgeRouteTrace.Bridge[] GetRouteGeometry()
        {
            lock(captureGate)
            {
                if(testCapture!=null)return routeGeometry;
                buildingIndex.Refresh();if(routeGeometryBuild==buildingIndex.Builds)return routeGeometry;
                var result=new List<BridgeRouteTrace.Bridge>();
                var buildings=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();var tiles=GameTileManagerAPI.Instance;
                foreach(var entry in buildingIndex.Entries)
                {
                    int slot=BridgeBuildingIndex.SpanIndex(entry.Id,buildings.Length);if(slot<0)continue;
                    ref GameBuilding b=ref buildings[slot];
                    if(b.r_GlobalId!=entry.Global||!BridgeSnapshot.Active(b.r_AliveState)||b.r_BuildingType!=eStructs.STRUCT_DRAWBRIDGE)continue;
                    var bridge=new BridgeRouteTrace.Bridge {Id=entry.Id,Global=entry.Global,Parent=entry.ParentId,ParentGlobal=entry.ParentGlobal,ParentLink=entry.Link};
                    int grid=(int)b.r_OccupyTileGridSize;
                    if(grid!=5||entry.Orientation<0||entry.Orientation>7) {Failure(new InvalidOperationException("Unresolved deck mapper: "+entry.Id+"/"+grid+"/"+entry.Orientation));continue;}
                    fixed(GameBuilding* ptr=&b)
                    {
                        uint* occupied=&ptr->r_OccupiedTileIdsArrayBegin;
                        for(int i=0;i<25;i++)if(Read(0x2D1A30+(entry.Orientation/2)*100+i*4)!=0&&(uint)occupied[i]<320800)
                        {var pos=tiles.GetTileVectorFromId((int)occupied[i]);if(BridgeRouteTrace.ValidXY(pos.X,pos.Y))bridge.Deck.Add(BridgeRouteTrace.Bridge.XY(pos.X,pos.Y));}
                    }
                    int parent=BridgeBuildingIndex.SpanIndex(entry.ParentId,buildings.Length);
                    if(parent>=0&&buildings[parent].r_GlobalId==entry.ParentGlobal&&BridgeSnapshot.Active(buildings[parent].r_AliveState)&&BridgeSnapshot.IsGate(buildings[parent].r_BuildingType))
                    {fixed(GameBuilding* ptr=&buildings[parent])foreach(int tile in BridgeSnapshot.Footprint(ptr))if((uint)tile<320800) {var pos=tiles.GetTileVectorFromId(tile);bridge.Gate.Add(BridgeRouteTrace.Bridge.XY(pos.X,pos.Y));}}
                    else {bridge.Parent=0;bridge.ParentGlobal=0;}
                    result.Add(bridge);
                }
                routeGeometry=result.ToArray();routeGeometryBuild=buildingIndex.Builds;return routeGeometry;
            }
        }
        private void UnboundRouteChange(long operation,int unit,long path,long priorCommand)
        {
            Scope root=CurrentScope;while(root!=null&&root.Site.Rva!=0x3C2E0)root=root.Parent;
            if(root!=null) {if(!CurrentScope.Detailed)Entry(CurrentScope,false);PromoteCommandChain?.Invoke(operation);}
            Emit("route-unbound-update","commandOp="+operation+",unitId="+unit+",pathDefinition="+path+",priorBoundCommand="+priorCommand+",planningRoot="+(root?.Id??0)+",decisionState="+(root==null?0:DecisionFor(root.PlanningPlayer,root.PrePlan))+",association=observed-post-state,preBytes=not-captured-for-this-command,causality=unresolved");
        }
        private void RouteEvidence(int id,uint global,int player,long plan,long root,long command,long decision,bool moved)
        {
            if(player<1||player>8)return;
            Scope activeRoot=CurrentScope;while(activeRoot!=null&&activeRoot.Site.Rva!=0x3C2E0)activeRoot=activeRoot.Parent;
            if(!moved&&root!=0&&activeRoot!=null&&activeRoot.Id==root)
            {
                if(!CurrentScope.Detailed)Entry(CurrentScope,false);
                PromoteCommandChain?.Invoke(command);
            }
            lock(captureGate)
            {
                if(!bridgeProgress.TryGetValue(id,out BridgeProgress bridge)||bridge.Global!=global)return;
                long[] values=moved?bridge.ExecutedRoutePlans:bridge.StoredRoutePlans;
                var roots=moved?bridge.ExecutedRouteRoots:bridge.StoredRouteRoots;var states=moved?bridge.ExecutedRouteDecisions:bridge.StoredRouteDecisions;
                bool changed=values[player]!=plan||roots[player]!=root||states[player]!=decision;values[player]=plan;roots[player]=root;states[player]=decision;
                if(!moved)bridge.StoredRoutePhysical[player]=bridge.PhysicalDefinition;
                if(changed)Emit("route-comparison","building="+id+"/g"+global+",player="+player+",candidatePlan="+plan+",planningRoot="+root+",commandOp="+command+",decisionState="+decision+",decisionLink="+(decision==0?"unresolved":"retained-completed-caller-state")+",stage="+(moved?"observed-deck-movement":"stored-deck-route")+",physicalDefinition="+bridge.PhysicalDefinition+",topologySettled="+!bridge.Pending+",candidateLink=chronological-unless-reservation-proven");
            }
        }
        private void CriticalObservation(string kind,string detail)
        {
            if(Volatile.Read(ref session)==0)return;
            if(kind=="route-capture-error")Interlocked.Increment(ref failures);
            Enqueue(new Record {Kind=kind,Detail=detail},kind!="route-background-repeat-batch");
        }
        private long TextDefinition(string category,string text)
        {
            lock(textGate)
            {
                string key=category+"\n"+text;
                if(textDefinitions.TryGetValue(key,out long value))return value;
                if(textDefinitions.Count>=4096)textDefinitions.Clear();
                value=++textId;textDefinitions.Add(key,value);
                Emit("text-definition","definition="+value+",category="+category+",text=["+text+"]");return value;
            }
        }
        internal void Aggregates(EnemyGatePathfindingTest.AiGateDecisionAggregate.RowSnapshot[] snapshots)
        {
            var rows=new StringBuilder();int count=0;
            foreach(var row in snapshots)
            {
                rows.Append(row.Player).Append('|').Append(row.GateId).Append('|').Append(TextDefinition("aggregate-stage",row.Stage)).Append('|').Append(TextDefinition("aggregate-result",row.Result)).Append('|').Append(row.Command).Append('|').Append(row.Count).Append('|').Append(row.Value).Append('|').Append(row.TargetChanges).Append('|').Append(EscapeRow(row.First)).Append('|').Append(EscapeRow(row.Last)).Append(';');
                if(++count==32) {Observe("aggregate-batch","columns=player/gate/stageDefinition/resultDefinition/command/count/value/targetChanges/first/last,separator=pipe,escape=backslash,rows=["+rows+"]");count=0;rows.Clear();}
            }
            if(count!=0)Observe("aggregate-batch","columns=player/gate/stageDefinition/resultDefinition/command/count/value/targetChanges/first/last,separator=pipe,escape=backslash,rows=["+rows+"]");
        }
        private static string EscapeRow(string text) => text.Replace("\\","\\\\").Replace("|","\\|").Replace(";","\\;").Replace("\r","\\r").Replace("\n","\\n");
        internal readonly struct CommandData
        {
            internal readonly long Op,Parent,Result,Regions,Searches,Failed,Following;
            internal readonly int Kind,Id,Tribe,Player,Command,X,Y,StartX,StartY,Source,Target,Type,Extra,Promoted,Near,Attribution;
            internal readonly uint Global;
            internal CommandData(long op,long parent=0,int kind=0,int id=0,uint global=0,int tribe=0,int player=0,int command=0,int x=0,int y=0,int startX=-1,int startY=-1,int source=0,int target=0,int type=0,int extra=0,long result=0,long regions=0,long searches=0,long failed=0,bool promoted=false,long following=0,bool near=false,int attribution=0)
            {Op=op;Parent=parent;Kind=kind;Id=id;Global=global;Tribe=tribe;Player=player;Command=command;X=x;Y=y;StartX=startX;StartY=startY;Source=source;Target=target;Type=type;Extra=extra;Result=result;Regions=regions;Searches=searches;Failed=failed;Promoted=promoted?1:0;Following=following;Near=near?1:0;Attribution=attribution;}
        }
        internal void Command(string kind,CommandData detail,int player=0)
        {
            if(Volatile.Read(ref session)==0) return;
            Safe(() =>
            {
                long prior=player>=1&&player<=8?lastPlayerPlan[player]:0;
                Scope context=CurrentScope;
                if(kind=="command-pre") {Interlocked.Increment(ref detailedCommands);if(prior!=0)Interlocked.Increment(ref followingCommands);}
                if(context!=null&&!context.Detailed) Entry(context,false);
                var key=new CommandContext(CurrentId,prior,prior!=0?lastPlayerPlanPhysical[player]:-1,prior!=0?lastPlayerPlanTopology[player]:-1,LiveState());
                long definition;
                lock(textGate)
                {
                    if(!commandContexts.TryGetValue(key,out definition))
                    {
                        if(commandContexts.Count>=4096)commandContexts.Clear();
                        definition=++commandContextId;commandContexts.Add(key,definition);
                        Emit("command-context","definition="+definition+",values=["+key+"],priorLink=chronological-not-proven-causality");
                    }
                }
                long identity;var identityKey=new Arguments(detail.Kind,detail.Id,unchecked((int)detail.Global),detail.Type,0,0);
                lock(textGate)if(!commandIdentities.TryGetValue(identityKey,out identity))
                {
                    if(commandIdentities.Count>=4096)commandIdentities.Clear();
                    identity=TextDefinition("command-identity","commandKind="+(detail.Kind==1?"target":detail.Kind==2?"move":"unit")+",id="+detail.Id+",global="+detail.Global+",typeRaw="+detail.Type);commandIdentities.Add(identityKey,identity);
                }
                var record=StampRecord(new Record());
                lock(textGate)
                {
                    if(kind=="command-post"&&commandPreRows.TryGetValue(detail.Op,out var pre)&&SameCommandInputs(pre.Data,detail))
                    {
                        commandRows.Append(record.Seq).Append('/').Append(record.Session).Append('/').Append(record.Thread).Append('/').Append(record.Tick).Append('/').Append(record.Clock).Append('/').Append(record.Physical).Append('/').Append(record.Topology).Append("/1/").Append(detail.Op).Append('/').Append(detail.Parent).Append('/').Append(definition).Append('/').Append(pre.Seq).Append('/').Append(detail.Result).Append('/').Append(detail.Regions).Append('/').Append(detail.Searches).Append('/').Append(detail.Failed).Append('/').Append(detail.Following).Append(';');
                    }
                    else
                    {
                    commandRows.Append(record.Seq).Append('/').Append(record.Session).Append('/').Append(record.Thread).Append('/').Append(record.Tick).Append('/').Append(record.Clock).Append('/').Append(record.Physical).Append('/').Append(record.Topology).Append('/').Append(kind=="command-pre"?0:1).Append('/').Append(detail.Op).Append('/').Append(detail.Parent).Append('/').Append(definition).Append('/').Append(identity)
                        .Append('/').Append(detail.Kind).Append('/').Append(detail.Tribe).Append('/').Append(detail.Player).Append('/').Append(detail.Command).Append('/').Append(detail.X).Append('/').Append(detail.Y).Append('/').Append(detail.StartX).Append('/').Append(detail.StartY).Append('/').Append(detail.Source).Append('/').Append(detail.Target).Append('/').Append(detail.Extra).Append('/').Append(detail.Result).Append('/').Append(detail.Regions).Append('/').Append(detail.Searches).Append('/').Append(detail.Failed).Append('/').Append(detail.Promoted).Append('/').Append(detail.Following).Append('/').Append(detail.Near).Append('/').Append(detail.Attribution).Append(';');
                    }
                    if(kind=="command-pre") {if(commandPreRows.Count>=4096)commandPreRows.Clear();commandPreRows[detail.Op]=new RetainedCommand(detail,record.Seq);}
                    else commandPreRows.Remove(detail.Op);
                    if(++commandRowCount==32)FlushCommandRows();
                }
            });
        }
        internal void SearchResult(long eventOp,string source,int? nativeResult,int effective,long calls,bool completed)
        {
            long sourceId=TextDefinition("search-source",source);var r=StampRecord(new Record());
            lock(textGate){searchRows.Append(r.Seq).Append('/').Append(r.Session).Append('/').Append(r.Thread).Append('/').Append(r.Tick).Append('/').Append(r.Clock).Append('/').Append(r.Physical).Append('/').Append(r.Topology).Append('/').Append(eventOp).Append('/').Append(CurrentId).Append('/').Append(sourceId).Append('/').Append(nativeResult.HasValue?1:0).Append('/').Append(nativeResult??0).Append('/').Append(effective).Append('/').Append(calls).Append('/').Append(completed?1:0).Append(';');if(++searchRowCount==32)FlushSearchRows();}
        }
        private void FlushSearchRows(){lock(textGate){if(searchRowCount==0)return;Enqueue(new Record {Kind="search-result-batch",Detail="columns=seq/session/thread/tick/clock/physical/topology/eventOp/parent/sourceDefinition/nativeKnown/native/effective/nativeCalls/completed,rows=["+searchRows+"],envelopeTiming=batch-flush"});searchRows.Clear();searchRowCount=0;}}
        private void FlushCommandRows(){lock(textGate){if(commandRowCount==0)return;Enqueue(new Record {Kind="command-frame-batch",Detail="schema=2,postReferenceColumns=seq/session/thread/tick/clock/physical/topology/post/op/parentEvent/commandContext/preSeq/result/regions/searches/failed/followingUnitOp,columns=seq/session/thread/tick/clock/physical/topology/post/op/parentEvent/commandContext/identityDefinition/commandKind/tribe/player/command/x/y/startX/startY/sourcePcl/targetPcl/extra/result/regions/searches/failed/promoted/followingUnitOp/nearBridge/attribution,rows=["+commandRows+"],envelopeTiming=batch-flush"});commandRows.Clear();commandRowCount=0;}}
        internal void CountCommand(bool pre) {if(pre) {Interlocked.Increment(ref commandPre);if(CurrentScope!=null)CurrentScope.Commands++;}else Interlocked.Increment(ref commandPost);}
        internal void TaskCommand(int unit,uint global,int tribe,int player,int command,long operation)
        {
            Scope scope=CurrentScope;if(scope==null)return;
            for(Scope parent=scope;parent!=null&&parent.Session==scope.Session;parent=parent.Parent)
                if(parent.Site.Rva==0x122B40&&parent.LadderBefore!=null)
                {
                    if(parent.LadderCommands.Count<4000)parent.LadderCommands.Add(new SelectionEvidence {Unit=unit,Global=global,Player=player,Movement=operation});
                    else Emit("task-followup-gap","consumer="+parent.Id+",reason=ladder-command-capacity");
                    break;
                }
            foreach(var evidence in scope.Selections)
                if(evidence.Unit==unit&&evidence.Global==global)
                {
                    evidence.Movement=operation;
                    Emit("task-following-command","parent="+scope.Id+",selection="+evidence.Op+",eventOp="+operation+",unit="+unit+"/g"+global+",tribe="+tribe+",player="+player+",command="+command+",candidatePlan="+(evidence.Unique?evidence.Plan:0)+",link=same-consumer-and-unit,routeAttribution=unproven");
                }
        }
        internal bool CommandIsNew(int kind,int id,uint global,Arguments state)
        {
            long key=((long)kind<<32)|(uint)id;
            lock(attackGate)
            {
                bool changed=!commands.TryGetValue(key,out CommandIdentity old)||old.Global!=global||!old.State.Equals(state);
                commands[key]=new CommandIdentity {Global=global,State=state};
                if(!changed)Interlocked.Increment(ref coalesced);
                return changed;
            }
        }
        internal void CountEvent(Arguments data)
        {
            Interlocked.Increment(ref backgroundCalls);
            lock(regionGate) {eventCounts.TryGetValue(data,out long count);eventCounts[data]=count+1;}
        }
        internal bool NearBridge(int x,int y)
        {
            if(testCapture!=null) return false;
            if((uint)x>=800||(uint)y>=800) return false;
            lock(captureGate)
            {
                buildingIndex.Refresh();
                var buildings=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                foreach(var entry in buildingIndex.Entries)
                {
                    int slot=BridgeBuildingIndex.SpanIndex(entry.Id,buildings.Length);if(slot<0)continue;
                    ref GameBuilding b=ref buildings[slot];
                    if(!BridgeBuildingIndex.IsCurrent(entry,b.r_GlobalId,b.r_PlayerIdOwner,b.r_GatehouseId,(int)b.r_OccupyTileGridSize,b.r_SpriteVariationIndex,b.r_TilePositionXBegin,b.r_TilePositionYBegin)) {buildingIndex.Invalidate();continue;}
                    if(entry.MaxX>=0&&x>=entry.MinX-1&&x<=entry.MaxX+1&&y>=entry.MinY-1&&y<=entry.MaxY+1)return true;
                }
                return false;
            }
        }
        internal void CountBackground(string category)
        {
            Interlocked.Increment(ref backgroundCalls);
            lock(regionGate) {backgroundCounts.TryGetValue(category,out long count);backgroundCounts[category]=count+1;}
        }
        internal void FlushCosts()
        {
            VirtualShadow?.FlushCosts();
            Interlocked.Exchange(ref unscopedGroupSamples,0);
            Emit("interval-route-cost",Routes.Interval());
            long now=Stopwatch.GetTimestamp(),c=captures,b=buildingIndex.Builds,r=coalesced,o=outputBytes,ct=captureTicks,dt=drainTicks,bg=backgroundCalls;
            Emit("interval-cost","seconds="+(lastCostClock==0?0:(now-lastCostClock)*1.0/Stopwatch.Frequency).ToString("F3")+",captures="+(c-lastCaptures)+",indexBuilds="+(b-lastBuilds)+",coalesced="+(r-lastCoalesced)+",outputBytes="+(o-lastOutputBytes)+",backgroundCalls="+(bg-lastBackgroundCalls)+",captureMs="+((ct-lastCaptureTicks)*1000.0/Stopwatch.Frequency).ToString("F3")+",drainMs="+((dt-lastDrainTicks)*1000.0/Stopwatch.Frequency).ToString("F3")+",queue="+queued+",criticalOverflow="+overflow+",backgroundOverflow="+backgroundOverflow);
            Emit("interval-index-cost","indexBuildMs="+((buildingIndex.BuildTicks-lastIndexTicks)*1000.0/Stopwatch.Frequency).ToString("F3")+",measurement=may-overlap-full-capture-time");
            Emit("interval-capture-cost","numericBridgeReads="+(numericReads-lastNumericReads)+",candidateReads="+(tableReads-lastTableReads)+",definitionFormats="+(definitionFormats-lastDefinitionFormats)+",tableReferences="+(tableReferences-lastTableReferences)+",readMs="+((readTicks-lastReadTicks)*1000.0/Stopwatch.Frequency).ToString("F3")+",compareMs="+((compareTicks-lastCompareTicks)*1000.0/Stopwatch.Frequency).ToString("F3")+",formatMs="+((formatTicks-lastFormatTicks)*1000.0/Stopwatch.Frequency).ToString("F3")+",measurement=bridge-and-candidate-observations,mayOverlapCaptureMs=True,includesLoggerPrefix=False");
            lastNumericReads=numericReads;lastDefinitionFormats=definitionFormats;lastTableReferences=tableReferences;
            lastTableReads=tableReads;
            lastReadTicks=readTicks;lastCompareTicks=compareTicks;lastFormatTicks=formatTicks;
            lastIndexTicks=buildingIndex.BuildTicks;
            lastCostClock=now;lastCaptures=c;lastBuilds=b;lastCoalesced=r;lastOutputBytes=o;lastCaptureTicks=ct;lastDrainTicks=dt;lastBackgroundCalls=bg;
            Emit("comparison-status","freshPlans="+freshPlans+",followingDetailedCommands="+followingCommands+",freshPlanAtCurrentPhysical="+(lastPlanPhysical==physical)+",freshPlanAtCurrentTopology="+(lastPlanTopology==topology)+",pending="+queued+",coverage="+(overflow==0&&failures==0&&incompleteCalls==0&&backgroundOverflow==0?"captured":"incomplete")+",missingPlanning="+(freshPlans==0)+",routeAttribution=unproven");
            lock(captureGate)foreach(var bridge in bridgeProgress)
                for(int player=1;player<=8;player++)
                    if(bridge.Value.Plans[player]!=0)
                        Emit("bridge-comparison-status","building="+bridge.Key+"/g"+bridge.Value.Global+",player="+player+",plan="+bridge.Value.Plans[player]+",topologySettled="+!bridge.Value.Pending+",freshPlanForRelevantFields="+(bridge.Value.PlanDefinitions[player]==bridge.Value.PhysicalDefinition)+",candidateSelectionObserved="+(bridge.Value.SelectedPlans[player]==bridge.Value.Plans[player])+",assignmentObserved="+(bridge.Value.AssignedPlans[player]==bridge.Value.Plans[player])+",movementCommandObserved="+(bridge.Value.CommandPlans[player]==bridge.Value.Plans[player])+",storedRouteObserved="+(bridge.Value.StoredRoutePlans[player]!=0&&bridge.Value.StoredRoutePlans[player]==bridge.Value.Plans[player])+",movementTransitionObserved="+(bridge.Value.ExecutedRoutePlans[player]!=0&&bridge.Value.ExecutedRoutePlans[player]==bridge.Value.Plans[player])+",routePlanLink=chronological-unless-reservation-proven,globalTopologyEqualityRequired=False,routeAttribution=unproven");
        }
        private void ArtifactObservation(long sourceSession,string kind,string detail)
        {
            var record=StampRecord(new Record {Kind=kind,Detail=detail+",envelopeTiming=delivery,inputTiming=binary-metadata"});record.Session=sourceSession;
            // This callback also runs after End; do not replace its historical session.
            Interlocked.Increment(ref queued);lines.Enqueue(record);
        }
        internal void Observe(string kind,string detail)
        {
            if(Volatile.Read(ref session)==0) return;
            Safe(() => Enqueue(new Record {Kind=kind,Detail="parent="+CurrentId+","+detail},false));
        }
        internal void Region(int player,int from,int to,int mode,int nativeResult,int effective)
        {
            Scope context=CurrentScope;
            if(context!=null) {context.Regions++;
                if(context.AccessValid&&player==context.Args[0]&&from==context.AccessInput[5]&&to==context.AccessInput[4])
                {context.AccessRegionObserved=true;context.AccessNative=nativeResult;context.AccessEffective=effective;}}

            if(Volatile.Read(ref session)==0) return;
            var key=new RegionKey(player,from,to,mode,nativeResult,effective);
            int regionId;
            lock(regionGate)
            {
                if(!regionIds.TryGetValue(key,out regionId)) {regionId=regionIds.Count+1;regionIds.Add(key,regionId);}
                regionCounts.TryGetValue(key,out RegionCount value);
                if(value.Count==0) value.FirstParent=CurrentId;
                value.Count++;value.LastParent=CurrentId;regionCounts[key]=value;
            }
            if(context!=null&&context.Detailed&&context.RegionSamples.Add(regionId))
                Enqueue(new Record {Kind="region",Parent=context.Id,Args=new Arguments(player,from,to,mode,nativeResult,effective)});
            else Interlocked.Increment(ref coalesced);
        }
        internal void FlushRegions()
        {
            Routes.Flush();FlushCommandRows();FlushSearchRows();FlushTableReferences();
            lock(regionGate)
            {
                var rows=new StringBuilder();int rowCount=0;
                foreach(var pair in regionCounts)
                {
                    rows.Append(pair.Key.Player).Append('/').Append(pair.Key.From).Append('/').Append(pair.Key.To).Append('/').Append(pair.Key.Mode).Append('/').Append(pair.Key.Native).Append('/').Append(pair.Key.Effective).Append('/').Append(pair.Value.Count).Append('/').Append(pair.Value.FirstParent).Append('/').Append(pair.Value.LastParent).Append(';');
                    if(++rowCount==32) {Enqueue(new Record {Kind="region-repeat",Detail="columns=player/from/to/mode/native/effective/count/firstParent/lastParent,rows=["+rows+"]"},false);rows.Clear();rowCount=0;}
                }
                if(rowCount>0)Enqueue(new Record {Kind="region-repeat",Detail="columns=player/from/to/mode/native/effective/count/firstParent/lastParent,rows=["+rows+"]"},false);
                regionCounts.Clear();
                foreach(var pair in backgroundCounts) Enqueue(new Record {Kind="background-count",Detail="category="+pair.Key+",count="+pair.Value},false);
                backgroundCounts.Clear();
                var events=new StringBuilder();int eventRows=0;
                foreach(var pair in eventCounts)
                {
                    events.Append(pair.Key).Append('/').Append(pair.Value).Append(';');
                    if(++eventRows==32) {Enqueue(new Record {Kind="event-count-batch",Detail="schema=kind4-search-kind5-keep-access-otherwise-command,columns=six-numeric-fields/count,rows=["+events+"]"},false);events.Clear();eventRows=0;}
                }
                if(eventRows!=0)Enqueue(new Record {Kind="event-count-batch",Detail="schema=kind4-search-kind5-keep-access-otherwise-command,columns=six-numeric-fields/count,rows=["+events+"]"},false);
                eventCounts.Clear();
            }
        }
        private void FlushTableReferences()
        {
            lock(captureGate)
            {
                if(tableRepeats.Count==0)return;
                var rows=new StringBuilder();int count=0;
                foreach(var pair in tableRepeats)
                {
                    var key=pair.Key;rows.Append(key.Parent).Append('/').Append(key.Rva).Append('/').Append(key.Slot).Append('/').Append(key.Definition).Append('/').Append(key.Plan).Append('/').Append(key.Phase).Append('/').Append(pair.Value).Append(';');
                    if(++count==32) {Emit("candidate-table-reference-batch","columns=parent/rva/slot/definition/plan/phase/count,rows=["+rows+"]");count=0;rows.Clear();}
                }
                if(count!=0)Emit("candidate-table-reference-batch","columns=parent/rva/slot/definition/plan/phase/count,rows=["+rows+"]");
                tableRepeats.Clear();
            }
        }
        private long LiveState()
        {
            long start=Stopwatch.GetTimestamp();
            try {Interlocked.Increment(ref captures);lock(captureGate) return testCapture!=null?testCapture():ReadLiveState();}
            finally {Interlocked.Add(ref captureTicks,Stopwatch.GetTimestamp()-start);}
        }
        private void Safe(Action action) {try {action();} catch(Exception error) {Failure(error);}}
        private void Failure(Exception error)
        {
            long count=Interlocked.Increment(ref failures);
            if(count==1||((count&(count-1))==0)) Emit("capture-error","count="+count+",type="+error.GetType().Name+",message="+error.Message);
        }
        private void Emit(string kind,string detail) => Enqueue(new Record {Kind=kind,Detail=detail});
        private void Enqueue(Record record,bool critical=true)
        {
            long pending=Interlocked.Increment(ref queued);
            if(critical)
            {
                if(pending-Interlocked.Read(ref backgroundQueued)>4096) {Interlocked.Decrement(ref queued);Interlocked.Increment(ref overflow);return;}
            }
            else if(Interlocked.Increment(ref backgroundQueued)>4096) {Interlocked.Decrement(ref queued);Interlocked.Decrement(ref backgroundQueued);Interlocked.Increment(ref backgroundOverflow);return;}
            if(record.Site!=null&&record.Context!=null)record.Context="contextDefinition="+TextDefinition("native-context",record.Context);
            record=StampRecord(record);
            if(critical)lines.Enqueue(record);else background.Enqueue(record);
        }
        private Record StampRecord(Record record)
        {
            record.Seq=Interlocked.Increment(ref sequence);record.Session=Volatile.Read(ref session);record.Thread=Thread.CurrentThread.ManagedThreadId;
            record.Tick=Interlocked.Read(ref tick);record.Clock=Stopwatch.GetTimestamp();record.Physical=Interlocked.Read(ref physical);record.Topology=Interlocked.Read(ref topology);return record;
        }
        internal void Drain()
        {
            long start=Stopwatch.GetTimestamp();int count=0;
            try
            {
                while(count<64&&(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency<2)
                {
                    bool critical=lines.TryDequeue(out Record r);
                    if(!critical&&!background.TryDequeue(out r))break;
                    if(!critical)Interlocked.Decrement(ref backgroundQueued);
                    Interlocked.Decrement(ref queued);
                    try {WriteRecord(r);}catch {Interlocked.Increment(ref deliveryErrors);}count++;
                }
                FlushNativeRows();
                if(count<64&&queued==0&&(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency<2)
                    lock(endedSessions)if(endedSessions.Count!=0)
                    {
                        var completion=endedSessions.Peek();long ended=completion.Session;if(VirtualShadow?.ArtifactPending(ended)==true)return;endedSessions.Dequeue();
                        var marker=StampRecord(new Record {Kind="session-delivered",Detail="captureComplete="+completion.CaptureComplete+",deliveryComplete="+(deliveryErrors==0)+",deliveryErrors="+deliveryErrors+",queue=0,"+(VirtualShadow?.ArtifactStatus(ended)??"inputArtifactsRequested=0")+",fileFlush=not-observed,waitForThisMarkerBeforeExit=True"});marker.Session=ended;
                        try {WriteRecord(marker);}catch {Interlocked.Increment(ref deliveryErrors);}
                    }
            }
            finally {Interlocked.Add(ref drainTicks,Stopwatch.GetTimestamp()-start);}
        }
        private readonly StringBuilder nativeRows=new StringBuilder();
        private Record nativeEnvelope;
        private int nativeRowCount;
        private void FlushNativeRows()
        {
            if(nativeRowCount==0)return;
            var record=nativeEnvelope;record.Site=null;record.Kind="native-frame-batch";
            record.Detail="columns=seq/session/thread/tick/clock/physical/topology/phase/rva/site/contextDefinition/values26,zeroRuns=zN,rows=["+nativeRows+"]";
            nativeRows.Clear();nativeRowCount=0;WriteRecord(record);
        }
        internal static string PackZeros(string row)
        {
            var tokens=row.Split('/');var packed=new StringBuilder();
            for(int i=0;i<tokens.Length;i++)
            {
                if(packed.Length!=0)packed.Append('/');
                int zeroes=0;while(i+zeroes<tokens.Length&&tokens[i+zeroes]=="0")zeroes++;
                if(zeroes>=3) {packed.Append('z').Append(zeroes);i+=zeroes-1;}else packed.Append(tokens[i]);
            }
            return packed.ToString();
        }
        private void WriteRecord(Record r)
        {
                    if(r.Site!=null)
                    {
                        if(nativeRowCount==0)nativeEnvelope=r;
                        string context=r.Context??"contextDefinition=0";
                        string row=r.Seq+"/"+r.Session+"/"+r.Thread+"/"+r.Tick+"/"+r.Clock+"/"+r.Physical+"/"+r.Topology+"/"+(r.Kind=="native-enter"?0:1)+"/"+r.Site.Rva.ToString("X")+"/"+r.Site.Name+"/"+context.Substring("contextDefinition=".Length)+"/"+
                            r.Op+"/"+r.Parent+"/"+r.Args+"/"+r.ScopeSession+"/"+r.State+"/"+r.Stamp.Global+"/"+r.Stamp.Owner+"/"+r.Stamp.Leader+"/"+r.Stamp.State+"/"+r.Stamp.Phase+"/"+r.Stamp.Target+"/"+r.Stamp.TargetGlobal+"/"+(r.Completed?1:0)+"/"+(r.Result?.ToString()??"v")+"/"+r.Regions+"/"+r.PlanInput;
                        nativeRows.Append(PackZeros(row)).Append(';');if(++nativeRowCount==16)FlushNativeRows();return;
                    }
                    string detail=r.Detail;
                    string kind=r.Kind;
                    if(r.Kind=="event-count") detail="columns="+(r.Args[0]==4?"searchKind/player/native/effective/completed/reserved":"kind/player/command/resultLow32/resultHigh32/phaseResolvedBits")+",values=["+r.Args+"],count="+r.Regions;
                    if(r.Kind=="region") detail="parent="+r.Parent+",player="+r.Args[0]+",from="+r.Args[1]+",to="+r.Args[2]+",mode="+r.Args[3]+",native="+r.Args[4]+",effective="+r.Args[5];
                    string line="bridge trace seq="+r.Seq+",session="+r.Session+",thread="+r.Thread+",tick="+r.Tick+",clock="+r.Clock+
                        ",physical="+r.Physical+",topology="+r.Topology+",kind="+kind+","+detail;
                    Shared.DebugLogHelper.LogInfo(log,line);Interlocked.Add(ref outputBytes,Encoding.UTF8.GetByteCount(line));Interlocked.Increment(ref outputRecords);
        }
        private int Read(int rva)
        {
            if(testRead!=null)return testRead(rva);
            if (native==IntPtr.Zero||nativeLength<4||rva<0||rva>nativeLength-4) throw new InvalidOperationException("Read outside native module: "+rva);
            return Marshal.ReadInt32(native,rva);
        }
        private string ReadOutput(IntPtr pointer,int offset,int count=3)
        {
            long rva=pointer.ToInt64()-native.ToInt64()+offset;
            if (rva<0||rva+count*4>nativeLength) return "unreadable-pointer=0x"+pointer.ToInt64().ToString("X");
            var text=new StringBuilder();
            for(int i=0;i<count;i++) text.Append(i==0?"":"/").Append(Read((int)rva+i*4));
            return text.ToString();
        }
        private string Context(BridgeNativeDefinition.Site site,Arguments args)
        {
            if (site.Name=="dispatch"||site.Name=="ordinary-attack"||site.Name=="consume-task")
            {
                int tribeId=args[0];
                if (!GameTribeManagerAPI.Instance.IsValidId(tribeId)||!GameTribeManagerAPI.Instance.TryGetTribeById(tribeId,out GameTribe* tribe)||tribe==null)
                    return "tribe="+tribeId+",identity=unresolved";
                // Raw manager-relative fields are audited in 10AA20/11A980, not struct-relative.
                int record=checked(0x7CC6720+tribeId*0x688);
                return "tribe="+tribeId+"/g"+tribe->r_GlobalId+",owner="+tribe->r_PlayerIdOwner+
                    ",leader="+tribe->r_LeaderUnitId+",state16="+(short)(Read(record+0x50)&65535)+
                    ",phase16="+(short)(Read(record+0x634)&65535)+",retainedTarget16="+(short)(Read(record+0x63C)&65535)+
                    ",targetGlobal="+unchecked((uint)Read(record+0x640));
            }
            int player=PlanningSite(site.Rva)?args[0]:CurrentScope?.PlanningPlayer??0;
            if(PlanningSite(site.Rva)||player!=0)
            {
                if(player<1||player>8)return "planningPlayerRaw="+player+",playerRecord=unresolved";
                int delta=player*0x583C;
                int attackerTile=Read(0x379AFB0+delta),targetPlayer=Read(0x379D9A8+delta);
                return "planningPlayer="+player+",playerRecordBase=0x379AE2C,recordStride=0x583C,planStamp=["+PlanStamp(player)+"],planStampColumns=lord/phase/targetPlayer/targetTile/x/y,attackerKeepTile="+attackerTile+",attackerKeepPcl="+ReadPcl(attackerTile)+",targetPlayer="+targetPlayer+",targetPlayerKeepTile="+(targetPlayer>=1&&targetPlayer<=8?Read(0x379AFB0+targetPlayer*0x583C):-1)+",fieldModeRaw="+(site.Rva==0x2D250?args[1]:-1)+",selectedTargetRegionRaw="+Read(0x2E97D10)+",targetPlayerKeepRegionGlobalRaw="+Read(0x2E97D14)+",globalTiming=last-writer-not-necessarily-this-player,componentCountEffective="+Read(0x2E9CA14)+",targetSeedRaw="+Read(0x2E97EA8)+",targetSeedModeRaw="+Read(0x2E97EAC);
            }
            return "activePlayerRaw="+Read(0x88E3D70)+",aiValueRaw="+Read(0x2E97E94)+
                ",selectedTargetRegionRaw="+Read(0x2E97D10)+",targetPlayerKeepRegionGlobalRaw="+Read(0x2E97D14)+
                ",componentCountRaw="+Read(0x2E9CA14);
        }
        private long ReadLiveState()
        {
            long started=Stopwatch.GetTimestamp();
            int player=CurrentScope?.PlanningPlayer??0;
            stateScratch.Count=0;stateScratch.Link=null;
            stateScratch.ObservedAt=started;
            stateScratch.Add(Read(0x60AD6CC));stateScratch.Add(Read(0x60AD6D4));stateScratch.Add(player);
            var buildings=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            var tiles=GameTileManagerAPI.Instance;
            var pcl=GamePathingManagerAPI.Instance.GetPathComponentGrid();
            var moat=tiles.GetMoatWorkTaskIndexLayer();
            buildingIndex.Refresh();
            foreach(var entry in buildingIndex.Entries)
            {
                int slot=BridgeBuildingIndex.SpanIndex(entry.Id,buildings.Length);
                if(slot<0) {buildingIndex.Invalidate();Failure(new InvalidOperationException("Indexed bridge missing: "+entry.Id));continue;}
                ref GameBuilding b=ref buildings[slot];
                if(!BridgeBuildingIndex.IsCurrent(entry,b.r_GlobalId,b.r_PlayerIdOwner,b.r_GatehouseId,(int)b.r_OccupyTileGridSize,b.r_SpriteVariationIndex,b.r_TilePositionXBegin,b.r_TilePositionYBegin))
                {buildingIndex.Invalidate();Failure(new InvalidOperationException("Indexed bridge identity changed: "+entry.Id));continue;}
                if(b.r_BuildingType!=eStructs.STRUCT_DRAWBRIDGE||!BridgeSnapshot.Active(b.r_AliveState))continue;
                physicalScratch.Count=planningScratch.Count=0;
                physicalScratch.ObservedAt=planningScratch.ObservedAt=Stopwatch.GetTimestamp();
                physicalScratch.Link=entry.Link;planningScratch.Link=null;
                // Numeric metadata: identity, roles, parent identity and footprint bounds.
                physicalScratch.Add(entry.Id);physicalScratch.Add(b.r_GlobalId);physicalScratch.Add(b.r_PlayerIdOwner);
                physicalScratch.Add(b.r_CapturedByPlayerId);physicalScratch.Add(b.r_GatehouseId);physicalScratch.Add((b.r_GateState | (b.r_GateState2 << 8)));
                physicalScratch.Add(b.r_SpriteVariationIndex);physicalScratch.Add(b.r_OccupyTileGridSize);
                int parent=BridgeBuildingIndex.SpanIndex(entry.ParentId,buildings.Length);
                bool validParent=parent>=0&&buildings[parent].r_GlobalId==entry.ParentGlobal&&BridgeSnapshot.Active(buildings[parent].r_AliveState)&&BridgeSnapshot.IsGate(buildings[parent].r_BuildingType);
                if(parent>=0&&!validParent) {buildingIndex.Invalidate();physicalScratch.Link="identity-mismatch";}
                physicalScratch.Add(validParent?entry.ParentId:0);physicalScratch.Add(validParent?entry.ParentGlobal:0);
                physicalScratch.Add(validParent?buildings[parent].r_PlayerIdOwner:0);physicalScratch.Add(validParent?buildings[parent].r_CapturedByPlayerId:0);
                int roles=0;
                if(validParent)for(int p=1;p<=8;p++)
                {if(BridgeSnapshot.Allied(p,buildings[parent].r_PlayerIdOwner))roles|=1<<(p-1);if(BridgeSnapshot.Allied(p,buildings[parent].r_CapturedByPlayerId))roles|=1<<(p+7);}
                physicalScratch.Add(roles);physicalScratch.Add(entry.MinX);physicalScratch.Add(entry.MinY);physicalScratch.Add(entry.MaxX);physicalScratch.Add(entry.MaxY);
                planningScratch.Add(entry.Id);planningScratch.Add(b.r_GlobalId);planningScratch.Add(player);
                int grid=(int)b.r_OccupyTileGridSize;
                if(grid<=0||grid>6) {Failure(new InvalidOperationException("Bridge footprint unreadable: "+grid));continue;}
                fixed(GameBuilding* pointer=&b)
                {
                    uint* occupied=&pointer->r_OccupiedTileIdsArrayBegin;
                    for(int i=0;i<grid*grid;i++)CaptureCell(occupied[i],player,pcl,moat,tiles);
                    for(int y=Math.Max(0,entry.MinY-1);y<=Math.Min(799,entry.MaxY+1);y++)
                        for(int x=Math.Max(0,entry.MinX-1);x<=Math.Min(799,entry.MaxX+1);x++)
                        {
                            if(x>=entry.MinX&&x<=entry.MaxX&&y>=entry.MinY&&y<=entry.MaxY)continue;
                            CaptureCell(tiles.GetTileId(x,y),player,pcl,moat,tiles);
                        }
                }
                long elapsed=Stopwatch.GetTimestamp();Interlocked.Add(ref readTicks,elapsed-started);Interlocked.Increment(ref numericReads);
                PublishBridgeImage(physicalScratch,planningScratch,player,CurrentScope?.Rebuilt==true);
                stateScratch.Add(entry.Id);stateScratch.Add(b.r_GlobalId);stateScratch.Add(bridgeProgress[entry.Id].PhysicalDefinition);stateScratch.Add(lastPlanningDefinition);
                started=Stopwatch.GetTimestamp();
            }
            Interlocked.Add(ref readTicks,Stopwatch.GetTimestamp()-started);
            long comparison=Stopwatch.GetTimestamp();bool known=numericStates.TryGetValue(stateScratch,out long id);Interlocked.Add(ref compareTicks,Stopwatch.GetTimestamp()-comparison);
            if(known)return id;
            id=++stateId;if(numericStates.Count>=4096)numericStates.Clear();numericStates.Add(stateScratch.Copy(),id);
            long format=Stopwatch.GetTimestamp();var state=new StringBuilder("dirty=").Append(stateScratch.Data[0]).Append(",pclRevision=").Append(stateScratch.Data[1]).Append(",distancePlayer=").Append(player).Append(",fragments=[");
            for(int i=3;i<stateScratch.Count;i+=4)state.Append(stateScratch.Data[i]).Append('/').Append(stateScratch.Data[i+1]).Append('/').Append(stateScratch.Data[i+2]).Append('/').Append(stateScratch.Data[i+3]).Append(';');
            Emit("live-state","state="+id+",capture=synchronous,captureClock="+stateScratch.ObservedAt+",captureTiming=read-start,attribution=indexed-live-bridges,"+state.Append("],columns=building/global/physicalDefinition/planningDefinition,referenceTiming=definition-original-observation-current"));
            Interlocked.Add(ref formatTicks,Stopwatch.GetTimestamp()-format);Interlocked.Increment(ref definitionFormats);return id;
        }
        private void CaptureCell(long tileRaw,int player,Span<ushort> pcl,Span<ushort> moat,GameTileManagerAPI tiles)
        {
            bool valid=tileRaw>=0&&tileRaw<pcl.Length&&tileRaw<moat.Length&&tileRaw<320800;
            int tile=valid?(int)tileRaw:0;
            physicalScratch.Add(tileRaw);physicalScratch.Add(valid?pcl[tile]:-1);physicalScratch.Add(valid?moat[tile]:-1);
            physicalScratch.Add(valid?(uint)tiles.GetTilePropertyFlag(tile):0);physicalScratch.Add(valid?Marshal.ReadByte(native,0x51890D0+tile):-1);
            planningScratch.Add(tileRaw);planningScratch.Add(valid?unchecked((sbyte)Marshal.ReadByte(native,0x535EF90+tile)):int.MinValue);
            planningScratch.Add(valid?Marshal.ReadByte(native,0x53AD4B0+tile):int.MinValue);
            long distance=0x5759230L+player*320800L*2+tile*2L;
            planningScratch.Add(valid&&player>=1&&player<=8&&distance>=0&&distance+2<=nativeLength?Marshal.ReadInt16(native,(int)distance):int.MinValue);
        }
        private long lastPlanningDefinition;
        internal void PublishBridgeImage(NumericImage physicalImage,NumericImage planningImage,int player,bool rebuilt)
        {
            long physicalId=NumericFragment(physicalDefinitions,"bridge-physical",physicalImage);
            lastPlanningDefinition=NumericFragment(planningDefinitions,"bridge-planning-fields",planningImage);
            int building=(int)physicalImage.Data[0];uint global=(uint)physicalImage.Data[1];
            if(!bridgeProgress.TryGetValue(building,out BridgeProgress progress)||progress.Global!=global)
            {
                bridgeProgress[building]=progress=new BridgeProgress {Global=global,Pending=testCapture==null&&Read(0x60AD6CC)!=0};
                for(int i=17;i<physicalImage.Count;i+=5)progress.Tiles.Add((int)physicalImage.Data[i]);
            }
            if(progress.PhysicalDefinition!=physicalId)
            {progress.Tiles.Clear();for(int i=17;i<physicalImage.Count;i+=5)progress.Tiles.Add((int)physicalImage.Data[i]);}
            progress.PhysicalDefinition=physicalId;if(rebuilt)progress.Pending=false;
        }
        private long NumericFragment(Dictionary<NumericImage,long> definitions,string kind,NumericImage image)
        {
            long comparison=Stopwatch.GetTimestamp();bool known=definitions.TryGetValue(image,out long id);Interlocked.Add(ref compareTicks,Stopwatch.GetTimestamp()-comparison);
            if(known)return id;
            if(definitions.Count>=4096)definitions.Clear();id=++fragmentId;definitions.Add(image.Copy(),id);
            long format=Stopwatch.GetTimestamp();string data=kind=="bridge-physical"?FormatPhysical(image):FormatPlanning(image);
            Emit(kind,"definition="+id+",captureClock="+(image.ObservedAt==0?format:image.ObservedAt)+",captureTiming=read-start,"+data);Interlocked.Add(ref formatTicks,Stopwatch.GetTimestamp()-format);Interlocked.Increment(ref definitionFormats);return id;
        }
        private static string FormatPhysical(NumericImage image)
        {
            var d=image.Data;var text=new StringBuilder("bridge=").Append(d[0]).Append("/g").Append(d[1]).Append(",owner=").Append(d[2]).Append(",capturerRaw=").Append(d[3]).Append(",connectionRecordRaw=").Append(d[4]).Append(",stateRaw=").Append(d[5]).Append(",orientation=").Append(d[6]).Append(",grid=").Append(d[7]).Append(",parentLink=").Append(image.Link);
            if(d[8]!=0)
            {
                text.Append(",parentCandidate=").Append(d[8]).Append("/g").Append(d[9]).Append(",parentOwner=").Append(d[10]).Append(",parentCapturer=").Append(d[11]).Append(",roles=[");
                for(int p=1;p<=8;p++)text.Append(p).Append(':').Append((d[12]&(1L<<(p-1)))!=0?"owner-or-ally":"enemy").Append('/').Append((d[12]&(1L<<(p+7)))!=0?"capturer-or-ally":"no-capture-access").Append(';');text.Append(']');
            }
            text.Append(",bounds=").Append(d[13]).Append('/').Append(d[14]).Append('/').Append(d[15]).Append('/').Append(d[16]).Append(",columns=tile/pcl/moat/flags/direction,cells=[");
            for(int i=17;i<image.Count;i+=5)text.Append(d[i]).Append('/').Append(d[i+1]).Append('/').Append(d[i+2]).Append('/').Append(d[i+3].ToString("X8")).Append('/').Append(d[i+4]).Append(';');
            return text.Append(']').ToString();
        }
        private static string FormatPlanning(NumericImage image)
        {
            var d=image.Data;var text=new StringBuilder("bridge=").Append(d[0]).Append("/g").Append(d[1]).Append(",player=").Append(d[2]).Append(",columns=tile/seed/field/distance16,cells=[");
            for(int i=3;i<image.Count;i+=4)text.Append(d[i]).Append('/').Append(d[i+1]).Append('/').Append(d[i+2]).Append('/').Append(d[i+3]==int.MinValue?"not-captured":d[i+3].ToString()).Append(';');return text.Append(']').ToString();
        }
        private int ReadPcl(int tile)
        {
            if((uint)tile>=320800)return int.MinValue;
            int rva=checked(0x50EC690+tile*2);
            return (short)(Read(rva)&65535);
        }
        private Arguments ReadAccessInput(IntPtr pointer,int attacker,int target)
        {
            if(attacker<1||attacker>8||target<1||target>8)throw new InvalidOperationException("Unresolved keep-access players: "+attacker+"/"+target);
            long manager=pointer.ToInt64()-native.ToInt64();
            if(manager<0||manager+9L*0x583C+0x12EDA4>nativeLength)throw new InvalidOperationException("Keep-access manager outside module");
            int a=checked((int)manager+attacker*0x583C),t=checked((int)manager+target*0x583C);
            int at=Read(a+0x12EDA0),tt=Read(t+0x12EDA0);
            return new Arguments(Read(a+0x12EC54),Read(t+0x12EC54),at,tt,ReadPcl(at),ReadPcl(tt));
        }
        internal static string AccessBranch(Arguments input)
        {
            if(input[0]==0)return "attacker-inactive-accept";
            if(input[1]==0)return "target-inactive-accept";
            if(input[4]==int.MinValue||input[5]==int.MinValue)return "unresolved-region";
            if(input[5]==0)return "target-zero-region-reject";
            if(input[4]==0)return "attacker-zero-region-reject";
            return input[4]==input[5]?"equal-nonzero-regions-accept":"different-regions-query-required";
        }
        private void MarkBridgePlanning(Scope scope)
        {
            int player=scope.PlanningPlayer;if(player<1||player>8)return;
            foreach(var pair in bridgeProgress)
            {
                pair.Value.Plans[player]=scope.Id;pair.Value.PlanDefinitions[player]=pair.Value.PhysicalDefinition;
                Emit("bridge-comparison","building="+pair.Key+"/g"+pair.Value.Global+",player="+player+",plan="+scope.Id+",physicalDefinition="+pair.Value.PhysicalDefinition+",topologySettled="+!pair.Value.Pending+",freshPlanning=True,taskSelection=not-yet-proven,routeAttribution=unproven");
            }
        }
        private TableImage ReadTable(int rva,int slot)
        {
            long started=Stopwatch.GetTimestamp();
            long at=(long)rva+slot*0x177BCL;
            if(slot<0||slot>8||at<0||at+16008>nativeLength)throw new InvalidOperationException("Candidate table outside module");
            int count=Read((int)at);if(count<0||count>1000)throw new InvalidOperationException("Candidate count outside audited capacity: "+count);
            var image=new TableImage {Rva=rva,Slot=slot,Count=count,Available=Read((int)at+4),Rows=new int[count*4]};
            for(int i=0;i<image.Rows.Length;i++)image.Rows[i]=Read((int)at+8+i*4);
            Interlocked.Add(ref readTicks,Stopwatch.GetTimestamp()-started);Interlocked.Increment(ref tableReads);
            return image;
        }
        private static readonly int[] CandidateTables={0x2EA70E4,0x2EAAF90,0x2EAEE2C,0x2EB2CC8,0x2EB6B64,0x2EBAA00};
        private void CaptureSelectionInput(Scope scope)
        {
            int unitId=scope.Args[0];var api=GameUnitManagerAPI.Instance;
            if(!api.IsValidId(unitId)||!APIShared.UnitAccess.TryGetById(api, unitId,out GameUnit* unit, out _)||unit==null)
            {Emit("task-followup-gap","selection="+scope.Id+",unitRaw="+unitId+",reason=invalid-selector-unit");return;}
            int player=unit->r_ControllableForPlayerId;
            if(player<1||player>8) {Emit("task-followup-gap","selection="+scope.Id+",playerRaw="+player+",reason=unresolved-selector-player");return;}
            scope.SelectedUnit=unitId;scope.SelectedGlobal=unit->r_GlobalId;
            // Standalone selectors resolve their actual unit owner, not an unrelated global player.
            scope.PlanningPlayer=player;
            foreach(int rva in CandidateTables)
            {
                Table(rva,player,1000,8,"selection-pre");
                scope.SelectionTables.Add(tableImages[((long)rva<<32)|(uint)player]);
            }
        }
        private void CompleteSelection(Scope scope,IntPtr pointer)
        {
            if(scope.SelectedUnit==0)return;
            int offset=checked((int)(pointer.ToInt64()-native.ToInt64()));
            if(offset<0||offset>nativeLength-12)throw new InvalidOperationException("Selector output outside module");
            int x=Read(offset),y=Read(offset+4),task=Read(offset+8);
            var evidence=new SelectionEvidence {Op=scope.Id,Unit=scope.SelectedUnit,Global=scope.SelectedGlobal,Player=scope.PlanningPlayer,Task=task,X=x,Y=y};
            int matches=0;
            foreach(TableImage before in scope.SelectionTables)
            {
                TableImage after=ReadTable(before.Rva,before.Slot);
                int matchCount=ReservationTransitions(before,after,scope.SelectedUnit,task,out int row);
                if(matchCount!=0) {matches+=matchCount;evidence.Table=before.Rva;evidence.Row=row;evidence.Definition=before.Definition;evidence.Plan=before.Plan;evidence.Approach=after.Rows[row*4+1];}
                PublishTable(after,"selection-post");
            }
            evidence.Unique=matches==1;
            if(evidence.Unique&&evidence.Plan!=0)foreach(var bridge in bridgeProgress.Values)
                if(bridge.Tiles.Contains(evidence.Task)||bridge.Tiles.Contains(evidence.Approach))bridge.SelectedPlans[evidence.Player]=evidence.Plan;
            Emit("task-selection","op="+scope.Id+",parent="+ParentId(scope)+",unit="+evidence.Unit+"/g"+evidence.Global+",player="+evidence.Player+",output="+x+"/"+y+"/"+task+",reservationMatches="+matches+",candidateDefinition="+(evidence.Unique?evidence.Definition:0)+",candidatePlan="+(evidence.Unique?evidence.Plan:0)+",table=0x"+evidence.Table.ToString("X")+",row="+evidence.Row+",link="+(evidence.Unique?"observed-reservation-transition":"unresolved")+",returnInterpretation=selector-specific-separate-from-output");
            Scope consumer=scope.Parent;
            if(consumer!=null&&consumer.Session==scope.Session&&consumer.Site.Rva==0x122B40)consumer.Selections.Add(evidence);
            else Emit("task-followup-gap","selection="+evidence.Op+",reason=no-observed-consume-task-parent");
        }
        // Consumer observations do not claim an internal selector return or Vanilla branch.
        internal static List<SelectionEvidence> LadderChanges(TableImage before,TableImage after)
        {
            var changes=new List<SelectionEvidence>();
            for(int row=0;row<after.Count;row++)
            {
                int unit=after.Rows[row*4+3];if(unit<=0)continue;
                if(row<before.Count&&before.Rows[row*4]==after.Rows[row*4]&&before.Rows[row*4+1]==after.Rows[row*4+1]&&before.Rows[row*4+3]==unit)continue;
                changes.Add(new SelectionEvidence {Unit=unit,Task=after.Rows[row*4],Approach=after.Rows[row*4+1],Player=after.Slot,Row=row,Table=after.Rva,Definition=after.Definition,Plan=after.Plan});
            }
            foreach(var evidence in changes)
            {
                int matches=0;for(int row=0;row<after.Count;row++)if(after.Rows[row*4+3]==evidence.Unit)matches++;
                evidence.Unique=matches==1;
            }
            return changes;
        }
        private void CompleteLadderAssignments(Scope scope)
        {
            if(scope.LadderBefore==null)return;
            var after=ReadTable(scope.LadderBefore.Rva,scope.LadderBefore.Slot);PublishTable(after,"ladder-consumer-post");
            foreach(var evidence in LadderChanges(scope.LadderBefore,after))
            {
                try
                {
                    GameUnit* unit=null;
                    if(testUnit!=null)unit=(GameUnit*)testUnit(evidence.Unit);
                    else if(!APIShared.UnitAccess.TryGetById(evidence.Unit,out unit,out _))unit=null;
                    int commands=0;SelectionEvidence command=null;
                    foreach(var observed in scope.LadderCommands)if(observed.Unit==evidence.Unit){commands++;command=observed;}
                    bool sameIdentity=unit!=null&&commands==1&&command.Global==unit->r_GlobalId&&command.Player==evidence.Player&&unit->r_ControllableForPlayerId==evidence.Player;
                    bool assigned=sameIdentity&&AssignmentMatches(unit,evidence.Task);
                    evidence.Global=unit==null?0:unit->r_GlobalId;evidence.Op=Interlocked.Increment(ref sequence);evidence.Unique&=assigned;
                    evidence.Movement=sameIdentity?command.Movement:0;
                    if(evidence.Unique)scope.Selections.Add(evidence);
                    Emit("ladder-consumer-assignment","op="+evidence.Op+",consumer="+scope.Id+",unit="+evidence.Unit+"/g"+evidence.Global+",player="+evidence.Player+",task="+evidence.Task+",approach="+evidence.Approach+",candidateDefinition="+evidence.Definition+",candidatePlan="+(evidence.Unique?evidence.Plan:0)+",movementEvent="+evidence.Movement+",missingMovement="+(commands==0)+",uniqueAssignment="+evidence.Unique+",sameCommandIdentity="+sameIdentity+",taskStillAssigned="+assigned+",evidence=effective-consumer-pre-post,selectorReturn=not-observed,selectionBranch=not-observed");
                    if(evidence.Unique)Emit("task-following-command","parent="+scope.Id+",selection="+evidence.Op+",eventOp="+evidence.Movement+",unit="+evidence.Unit+"/g"+evidence.Global+",player="+evidence.Player+",candidatePlan="+evidence.Plan+",link=same-consumer-and-unit,observation=consumer-return,selectorReturn=not-observed,routeAttribution=unproven");
                }
                catch(Exception error){Failure(error);Emit("task-followup-gap","consumer="+scope.Id+",unit="+evidence.Unit+",reason=ladder-consumer-capture-failure");}
            }
        }
        private void CompleteTaskAssignments(Scope scope)
        {
            foreach(var evidence in scope.Selections)
            {
                try
                {
                GameUnit* unit=null;
                if(testUnit!=null)unit=(GameUnit*)testUnit(evidence.Unit);
                else
                {
                    var api=GameUnitManagerAPI.Instance;
                    if(!APIShared.UnitAccess.TryGetById(api, evidence.Unit,out unit, out _))
                    {Emit("task-assignment","selection="+evidence.Op+",identity=unresolved");continue;}
                }
                if(unit==null||unit->r_GlobalId!=evidence.Global)
                {Emit("task-assignment","selection="+evidence.Op+",identity=reused-or-unresolved");continue;}
                int command=unit->r_AI_LastIssuedTribeCommand;
                uint assigned=ReadAssignedTask(unit);
                int aiState=unit->r_AIState;
                if(evidence.Unique&&evidence.Plan!=0&&assigned==evidence.Task&&AssignmentMatches(unit,evidence.Task))foreach(var bridge in bridgeProgress.Values)
                    if(bridge.Tiles.Contains(evidence.Task)||bridge.Tiles.Contains(evidence.Approach))
                    {bridge.AssignedPlans[evidence.Player]=evidence.Plan;if(evidence.Movement!=0)bridge.CommandPlans[evidence.Player]=evidence.Plan;}
                Emit("task-assignment","parent="+scope.Id+",selection="+evidence.Op+",unit="+evidence.Unit+"/g"+evidence.Global+",tribe="+unit->r_TribeId+",player="+(unit->r_ControllableForPlayerId)+",command="+command+",context="+unit->r_ContextTargetTileX+"/"+unit->r_ContextTargetTileY+",aiState="+aiState+",selectedTask="+evidence.Task+",nativeAssignedTaskRaw="+assigned+",candidatePlan="+(evidence.Unique?evidence.Plan:0)+",movementEvent="+evidence.Movement+",missingMovement="+(evidence.Movement==0)+",stage=consumer-return,execution=not-proven");
                }
                catch(Exception error) {Failure(error);Emit("task-assignment-gap","parent="+scope.Id+",selection="+evidence.Op+",unit="+evidence.Unit+"/g"+evidence.Global+",reason=capture-failure");}
            }
        }
        internal static uint ReadAssignedTask(GameUnit* unit) => unit->r_AI_ContextTargetBuildingTileId;
        internal static bool AssignmentMatches(GameUnit* unit,int task) => ReadAssignedTask(unit)==unchecked((uint)task)&&(unit->r_AIState==0x65||unit->r_AIState==0x67);
        internal static int ReservationTransitions(TableImage before,TableImage after,int unit,int task,out int selectedRow)
        {
            int matches=0;selectedRow=-1;if(unit<=0)return 0;
            for(int row=0;row<after.Count;row++)
                if(after.Rows[row*4]==task&&after.Rows[row*4+3]==unit&&
                    (row>=before.Count||before.Rows[row*4+3]!=unit)) {matches++;selectedRow=row;}
            return matches;
        }
        internal bool BridgePlanCurrent(int building,uint global,int player)
        {
            lock(captureGate)return player>=1&&player<=8&&bridgeProgress.TryGetValue(building,out BridgeProgress bridge)&&bridge.Global==global&&!bridge.Pending&&bridge.Plans[player]!=0&&bridge.PlanDefinitions[player]==bridge.PhysicalDefinition;
        }
        private void CaptureTables(BridgeNativeDefinition.Site site,Arguments args,string phase,IntPtr pointer)
        {
            if(testCapture!=null)return;
            if(site.Rva==0x3C2E0&&args[0]>=1&&args[0]<=8) CapturePlayerGroups(args[0],phase);
            if(site.Name=="candidates"||site.Name=="weights"||site.Rva==0x2C480)
            {
                int slot=site.Name=="weights"?args[1]:args[0];
                foreach(var table in new[]{0x2EA70E4,0x2EAAF90,0x2EAEE2C,0x2EB2CC8,0x2EB6B64,0x2EBAA00})
                    Table(table,slot,1000,8,phase);
                Table(0x2EB9668,0,1000,8,phase,false);
                Table(0x2E8A830,0,1000,8,phase,false);
                Table(0x2E8E6CC,0,1000,8,phase,false);
            }
            if(site.Name=="assign-groups"||site.Name=="assign-reachable-groups")
            {
                int count=Read(0x2EBD500); var text=new StringBuilder("countRaw=").Append(count).Append(",groups=[");
                if(count>=0&&count<=250)
                    for(int i=0;i<count;i++)
                    {
                        int tribe=Read(0x2EBD928+i*4); text.Append(tribe).Append(':');
                        if(GameTribeManagerAPI.Instance.IsValidId(tribe)&&GameTribeManagerAPI.Instance.TryGetTribeById(tribe,out GameTribe* value)&&value!=null)
                            text.Append(value->r_GlobalId).Append('/').Append(value->r_PlayerIdOwner).Append('/').Append((short)(Read(checked(0x7CC6770+tribe*0x688))&65535));
                        else text.Append("unresolved");
                        text.Append(';');
                    }
                else text.Append("unreadable-capacity");
                Emit("group-assignment","parent="+CurrentId+",phase="+phase+","+text.Append(']'));
            }
        }
        private void CapturePlayerGroups(int player,string phase)
        {
            int delta=player*0x583C;groupScratch.Count=0;groupScratch.Link=null;
            var api=GameTribeManagerAPI.Instance;
            for(int slot=0;slot<200;slot++)
            {
                int id=(short)(Read(0x379F670+delta+slot*2)&65535);if(id==0)continue;
                uint expected=unchecked((uint)Read(0x379F8C8+delta+slot*4));
                groupScratch.Add(slot);groupScratch.Add(id);groupScratch.Add(expected);
                bool valid=api.IsValidId(id)&&api.TryGetTribeById(id,out GameTribe* live)&&live!=null&&live->r_GlobalId==expected;
                AttackStamp stamp=valid?ReadStamp(id):default;
                groupScratch.Add(valid?(long)stamp.Global:-1);groupScratch.Add(stamp.Owner);groupScratch.Add(stamp.Leader);
                groupScratch.Add(stamp.State);groupScratch.Add(stamp.Phase);groupScratch.Add(stamp.Target);groupScratch.Add(stamp.TargetGlobal);
            }
            if(!groupDefinitions.TryGetValue(groupScratch,out long idDefinition))
            {
                if(groupDefinitions.Count>=4096)groupDefinitions.Clear();
                idDefinition=++groupDefinition;groupDefinitions.Add(groupScratch.Copy(),idDefinition);
                var rows=new StringBuilder();for(int i=0;i<groupScratch.Count;i++)rows.Append(groupScratch.Data[i]).Append(i%10==9?';':'/');
                Emit("player-group-definition","definition="+idDefinition+",columns=slot/tribe/expectedGlobal/liveGlobal/owner/leader/state/phase/target/targetGlobal,rows=["+rows+"]");
            }
            Emit("player-groups","parent="+CurrentId+",player="+player+",phase="+phase+",definition="+idDefinition);
        }
        private void Table(int rva,int slot,int capacity,int rowOffset,string phase,bool indexed=true)
        {
            // All audited tables use count/header plus four numeric columns per row.
            int actualSlot=indexed?slot:0;
            TableImage image=ReadTable(rva,actualSlot);
            PublishTable(image,phase);
        }
        internal void PublishTable(TableImage image,string phase)
        {
            long started=Stopwatch.GetTimestamp();
            long key=((long)image.Rva<<32)|(uint)image.Slot;
            tableImages.TryGetValue(key,out TableImage old);
            bool equal=old!=null&&old.Count==image.Count&&old.Available==image.Available;
            int changed=0;
            for(int row=0;row<image.Count;row++)
            {
                bool different=old==null||row>=old.Count;
                if(!different)for(int c=0;c<4;c++)different|=old.Rows[row*4+c]!=image.Rows[row*4+c];
                if(!different)continue;
                equal=false;changed++;
            }
            candidateScratch.Count=0;candidateScratch.Add(image.Count);candidateScratch.Add(image.Available);
            for(int i=0;i<image.Rows.Length;i++)candidateScratch.Add(image.Rows[i]);
            bool reused=candidateDefinitions.TryGetValue(candidateScratch,out long reusedDefinition);
            if(equal||reused)
            {
                Interlocked.Add(ref compareTicks,Stopwatch.GetTimestamp()-started);
                image.Definition=reused?reusedDefinition:old.Definition;image.Plan=old?.Plan??0;
                // A fresh rebuild of an equal table still has a new provenance.
                if(CurrentScope?.Site.Rva==0x2C480&&phase=="post")image.Plan=CurrentId;
                tableImages[key]=image;Interlocked.Increment(ref coalesced);
                lock(captureGate)
                {
                    var reference=new TableReference(CurrentId,image,phase);tableRepeats.TryGetValue(reference,out long repeats);tableRepeats[reference]=repeats+1;tableReferences++;
                    if(tableRepeats.Count>=128)FlushTableReferences();
                }
                return;
            }
            Interlocked.Add(ref compareTicks,Stopwatch.GetTimestamp()-started);started=Stopwatch.GetTimestamp();
            image.Definition=++tableId;image.Plan=old?.Plan??0;
            if(candidateDefinitions.Count>=4096)candidateDefinitions.Clear();candidateDefinitions.Add(candidateScratch.Copy(),image.Definition);
            if(CurrentScope?.Site.Rva==0x2C480&&phase=="post")image.Plan=CurrentId;
            bool delta=old!=null&&changed*2<=image.Count;
            var rows=new StringBuilder();
            for(int row=0;row<image.Count;row++)
            {
                if(delta)
                {
                    bool different=row>=old.Count;
                    if(!different)for(int c=0;c<4;c++)different|=old.Rows[row*4+c]!=image.Rows[row*4+c];
                    if(!different)continue;rows.Append(row).Append(':');
                }
                for(int c=0;c<4;c++)rows.Append(image.Rows[row*4+c]).Append(c==3?';':'/');
            }
            Emit("candidate-table","definition="+image.Definition+",parent="+CurrentId+",phase="+phase+",table=0x"+image.Rva.ToString("X")+",slotRaw="+image.Slot+",plan="+image.Plan+",countRaw="+image.Count+",headerPlus4Raw="+image.Available+",encoding="+(delta?"row-delta":"full")+",baseDefinition="+(delta?old.Definition:0)+",columns=taskTile/approachTile/weight/reservedUnit,truncateToCount=True,rawRows=["+rows+"]");
            tableImages[key]=image;
            Interlocked.Add(ref formatTicks,Stopwatch.GetTimestamp()-started);Interlocked.Increment(ref definitionFormats);
        }
    }
}
