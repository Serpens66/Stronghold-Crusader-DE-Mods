using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace EnemyBridgePathTest
{
    // Simulation-thread capture, render-thread pure managed work. Both are serialized
    // by gate; no Span/pointer or live member crosses the capture boundary.
    internal sealed unsafe class BridgeVirtualShadow
    {
        private readonly object gate=new object();
        private readonly Action<string,string> emit;
        private readonly Func<int,int> read;
        private readonly LinkedList<Request> pending=new LinkedList<Request>();
        private readonly Dictionary<QueryKey,Repeat> repeats=new Dictionary<QueryKey,Repeat>();
        private struct Repeat {internal long Id,Op,Parent,Count,LastOp;}
        private readonly struct QueryKey : IEquatable<QueryKey>
        {
            private readonly string stage;
            private readonly int player,from,to,mode,result;
            private readonly long revision,identity;
            private readonly IEnemyGateRoutePolicySnapshot policy;
            private readonly bool policyValid;
            private readonly object content;
            internal QueryKey(string stage,int player,int from,int to,int mode,int result,long revision,long identity,IEnemyGateRoutePolicySnapshot policy,bool policyValid,Captured content=null)
            {this.content=content?.Token??content;this.policy=policy;this.policyValid=policyValid;this.stage=stage;this.player=player;this.from=from;this.to=to;this.mode=mode;this.result=result;this.revision=revision;this.identity=identity;}
            public bool Equals(QueryKey b) => stage==b.stage&&player==b.player&&from==b.from&&to==b.to&&mode==b.mode&&result==b.result&&(content==null&&b.content==null?revision==b.revision&&identity==b.identity:ReferenceEquals(content,b.content))&&ReferenceEquals(policy,b.policy)&&policyValid==b.policyValid;
            public override bool Equals(object b) => b is QueryKey value&&Equals(value);
            public override int GetHashCode() => unchecked(stage.GetHashCode()*397^player*31^from*11^to^mode*17^result^(content==null?revision.GetHashCode()^identity.GetHashCode():System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(content)));
        }
        private long queryId,session,revision=-1,identity,captureTicks,computeTicks,captures,coalesced,rejected,completedQueries;
        private long costClock,lastCaptureTicks,lastComputeTicks,lastCaptures,lastCoalesced,lastRejected,lastArtifactBytes,lastArtifactTicks;
        private Captured captured,lastContent;
        private long decisionCaptureTicks,decisionCaptures,lastDecisionCaptureTicks,lastDecisionCaptures;
        private readonly Dictionary<QueryKey,Preparation[]> preparations=new Dictionary<QueryKey,Preparation[]>();
        private VirtualBridgeWorkspace workspace;
        private long attempted,backgroundDeferred,prepareTicks,searchTicks,formatTicks,comparisonTicks,preparationHits,contentReused,validationTicks,retainedGroupEvictions,unresolvedGroupLinks;
        private long lastPrepareTicks,lastSearchTicks,lastFormatTicks,lastComparisonTicks,lastPreparationHits,lastContentReused,lastDeferred,lastValidationTicks;
        private sealed class Preparation
        {internal VirtualBridgeMap Map;internal int[] Deck;internal VirtualComponentLink[] Links;internal int Unknown;internal bool Authorized,Boundary;}
        private string missingInput="awaiting-observed-rebuild";
        private Request active;
        private ushort[] coordinateX,coordinateY;
        private int[] coordinateRows;
        private readonly BridgeInputArtifact artifacts;
        private readonly Dictionary<long,Request> groups=new Dictionary<long,Request>();
        private bool keepArtifact,groupArtifact;
        private Request planningRequest;
        private Captured decisionCaptured;
        private long decisionCaptureOp;
        private BridgeBuildingIndex decisionIndex;
        private long planningBundleOp,planningBundleDefinition,expectedPlanningFamily;
        internal void ExpectPlanningFamily(long op) {lock(gate)expectedPlanningFamily=op;}
        internal object PlanningInputToken {get {lock(gate)return captured?.Token;}}
        internal bool PreparePlanning(BridgePlanningCapture.Bundle bundle,BridgeBuildingIndex index,bool decisionOnly=false)
        {
            lock(gate)
            {
                decisionCaptured=null;decisionCaptureOp=bundle.Op;decisionIndex=index;
                Rebuilt(bundle.Session,bundle.Revision,index,true);
                var input=decisionCaptured;
                if(input==null||input.Session!=bundle.Session||input.NativeRevision!=bundle.Revision||input.Dirty!=bundle.Dirty)return false;
                foreach(var pair in new[]{new KeyValuePair<string,Array>("flags",input.Flags),new KeyValuePair<string,Array>("components",input.Pcl),new KeyValuePair<string,Array>("edges",input.Edges)})
                {var copy=new byte[Buffer.ByteLength(pair.Value)];Buffer.BlockCopy(pair.Value,0,copy,0,copy.Length);byte[] original=CopiedPlanningBundle.Resolve(bundle,pair.Key);if(copy.Length!=original.Length)return false;for(int i=0;i<copy.Length;i++)if(copy[i]!=original[i])return false;}
                emit("planning-input-copied","op="+bundle.Op+",dirty="+bundle.Dirty+",nativeRevision="+bundle.Revision+",captureClock="+input.Clock+",decisionCapture=True,pendingTopology="+(bundle.Dirty!=0)+",negativeEligible=False,behavior=unchanged");return true;
            }
        }
        internal bool PlanningCompleted(BridgePlanningCapture.Bundle bundle)
        {
            lock(gate)
            {
                var request=planningRequest;
                if(keepArtifact||!bundle.Complete||request==null||request.Parent!=bundle.Op||decisionCaptureOp!=bundle.Op||decisionCaptured==null||decisionCaptured.Session!=bundle.Session)
                {emit("planning-artifact-unresolved","op="+bundle.Op+",reason=no-matching-historical-keep-input");return false;}
                request=request.ArtifactCopy();request.Input=decisionCaptured;request.InputReason="own-decision-copy-pending-topology-"+bundle.Dirty;keepArtifact=artifacts.Enqueue(bundle.Session,request.Id,Artifact(request,bundle),2);
                if(keepArtifact)
                {
                    planningBundleOp=bundle.Root;planningBundleDefinition=request.Id;
                    Request first=null;foreach(var group in groups.Values)if(group.BridgeRelevant&&group.Player==8&&group.Input!=null&&group.PolicyValidAtDecision&&(group.PlanningRoot==planningBundleOp||group.DecisionRoot==planningBundleOp)&&(first==null||group.Clock<first.Clock))first=group;
                    if(first!=null)groupArtifact=artifacts.Enqueue(first.Input.Session,first.Id,Artifact(first),2);
                }
                emit("planning-artifact-selected","op="+bundle.Op+",definition="+request.Id+",complete="+bundle.Complete+",accepted="+keepArtifact+",captureMs="+(bundle.Ticks*1000.0/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
                planningRequest=null;
                return keepArtifact;
            }
        }
        internal bool ArtifactPending(long value) {lock(gate)return artifacts.Pending(value);}
        internal void PumpArtifacts() {lock(gate)artifacts.Pump();}
        internal string ArtifactStatus(long value) {lock(gate)return artifacts.Status(value);}
        private sealed class Captured
        {
            internal long Session,Revision,Identity,Clock;
            internal int Dirty,NativeRevision;
            internal object Token;
            internal VirtualBridgeMap Map;
            internal ushort[] Pcl,X,Y,SpecialIds;
            internal short[] SpecialKinds;
            internal byte[] Edges;
            internal int[] Flags,Rows;
            internal PathConnectionRecord[] Records;
            internal int[] Anchors;
            internal Deck[] Decks;
            internal int[] Owners,Capturers;
            internal int ConnectionLimit;
            internal uint[] Globals,UnitGlobals;
            internal bool[,] Allies;
        }
        // NativeParent retains schema1 slot meaning: opaque r_GatehouseId, not a parent ID.
        private sealed class Deck {internal int Id,Owner,Capturer,NativeParent,ParentId,ParentOwner,ParentCapturer;internal uint Global,ParentGlobal;internal int[] Tiles;internal bool Authorized,BoundaryVerified;}
        private sealed class Request
        {
            internal string Stage,InputReason;
            internal Request Alternative;
            internal bool Scheduled,BridgeRelevant;
            internal Request ArtifactCopy() => (Request)MemberwiseClone();
            internal long Id,Op,Parent,Clock,QuerySourceOp,PlanningRoot,Decision,DecisionRoot;
            internal int Tribe,Phase=-1;
            internal uint TribeGlobal;
            internal int Player,From,To,Mode,Native,UnknownRecords,DeckCells,FromX,FromY,ToX,ToY;
            internal bool XYKnown;
            internal Captured Input;
            internal IEnemyGateRoutePolicySnapshot GatePolicy;
            internal VirtualBridgeQuery Query;
            internal bool Authorization,PolicyValidAtDecision;
            internal int Variant;
            internal VirtualReachability Macro=VirtualReachability.Unknown;
            internal readonly VirtualReachability[] Results=new VirtualReachability[6];
            internal readonly string[] Reasons=new string[6];
            internal readonly int[] Cuts=new int[6];
            internal readonly long[] Expansions=new long[6];
        }
        internal BridgeVirtualShadow(Action<string,string> emit,Func<int,int> read,Action<long,string,string> artifactEmit=null,string artifactDirectory=null)
        {
            this.emit=emit;this.read=read;
            string folder=artifactDirectory??System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(BridgeVirtualShadow).Assembly.Location),"..","..","diagnostics","EnemyBridgePathTest",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")));
            artifacts=new BridgeInputArtifact(folder,artifactEmit??((_,kind,detail)=>emit(kind,detail)));
        }
        internal void Begin(long value) {lock(gate) {session=value;revision=-1;captured=null;coordinateX=coordinateY=null;coordinateRows=null;missingInput="awaiting-observed-rebuild";pending.Clear();active=null;repeats.Clear();groups.Clear();preparations.Clear();lastContent=null;workspace=null;keepArtifact=groupArtifact=false;planningRequest=null;decisionCaptured=null;decisionCaptureOp=0;decisionIndex=null;planningBundleOp=planningBundleDefinition=expectedPlanningFamily=0;identity++;decisionCaptureTicks=decisionCaptures=lastDecisionCaptureTicks=lastDecisionCaptures=0;completedQueries=captures=coalesced=rejected=captureTicks=computeTicks=0;costClock=lastCaptureTicks=lastComputeTicks=lastCaptures=lastCoalesced=lastRejected=0;lastArtifactBytes=artifacts.Bytes;lastArtifactTicks=artifacts.Ticks;attempted=backgroundDeferred=prepareTicks=searchTicks=formatTicks=comparisonTicks=preparationHits=contentReused=validationTicks=retainedGroupEvictions=unresolvedGroupLinks=0;lastPrepareTicks=lastSearchTicks=lastFormatTicks=lastComparisonTicks=lastPreparationHits=lastContentReused=lastDeferred=lastValidationTicks=0;}}
        internal void Invalidate() {lock(gate) {identity++;captured=null;missingInput="identity-or-physical-invalidation-awaiting-rebuild";}}
        internal void End()
        {
            lock(gate)
            {
                FlushRepeats();
                var cancelled=new System.Text.StringBuilder();if(active!=null)cancelled.Append(active.Id).Append(';');foreach(var request in pending)cancelled.Append(request.Id).Append(';');
                emit("virtual-shadow-end","decisionInputCopies="+decisionCaptures+",decisionInputCopyMs="+Ms(decisionCaptureTicks)+",shadowSession="+session+",captureCount="+captures+",completedQueries="+completedQueries+",captureMs="+Ms(captureTicks)+",computeMs="+Ms(computeTicks)+",prepareMs="+Ms(prepareTicks)+",searchMs="+Ms(searchTicks)+",formatMs="+Ms(formatTicks)+",validationMs="+Ms(validationTicks)+",retainedGroupEvictions="+retainedGroupEvictions+",unresolvedGroupLinks="+unresolvedGroupLinks+",contentCompareMs="+Ms(comparisonTicks)+",preparationHits="+preparationHits+",contentReused="+contentReused+",attempted="+attempted+",backgroundDeferred="+backgroundDeferred+",comparisonScope=fresh-keeps-selected-targets-proven-bridge-groups,coalesced="+coalesced+",rejected="+rejected+",pending="+(pending.Count+(active==null?0:1))+",pendingOutcome=cancelled-at-session-end,cancelledDefinitions=["+cancelled+"],keepArtifactSelected="+keepArtifact+",bridgeGroupArtifactSelected="+groupArtifact+","+artifacts.Status(session)+",behavior=unchanged");
                session=0;captured=lastContent=null;pending.Clear();groups.Clear();preparations.Clear();repeats.Clear();workspace=null;active=null;
            }
        }
        private static string Ms(long ticks) => (ticks*1000.0/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
        internal void Rebuilt(long traceSession,long traceRevision,BridgeBuildingIndex index,bool decisionOnly=false)
        {
            lock(gate)
            {
                if(!decisionOnly){revision=traceRevision;captured=null;missingInput="post-rebuild-capture-unavailable";}
                if(session==0||session!=traceSession)return;
                long now=Stopwatch.GetTimestamp();
                long start=now;
                try
                {
                    // The audited E49D0 post is the only publication opportunity.
                    int nativeRevision=read(0x60AD6D4);
                    int dirty=read(0x60AD6CC);if(!decisionOnly&&dirty!=0)return;
                    var path=GamePathingManagerAPI.Instance;var tiles=GameTileManagerAPI.Instance;
                    var pcl=path.GetPathComponentGrid();var edges=path.GetPathEdgeMaskGrid();var flags=tiles.GetLogicLayer();
                    if(pcl.Length!=320800||edges.Length!=pcl.Length||flags.Length!=pcl.Length)throw new InvalidOperationException("Packed capacity changed");
                    var result=new Captured {Session=session,Revision=traceRevision,NativeRevision=nativeRevision,Dirty=dirty,Identity=identity,Clock=now,Pcl=pcl.ToArray(),Edges=edges.ToArray(),Flags=flags.ToArray(),Anchors=new int[65536]};
                    for(int i=0;i<result.Anchors.Length;i++)result.Anchors[i]=-1;
                    if(tiles.MapRowLookupTable==null||tiles.MapColumnLookupTable==null)throw new InvalidOperationException("Packed coordinate view absent");
                    bool sameCoordinates=coordinateRows!=null&&coordinateY.Length==pcl.Length;
                    for(int y=0;y<800&&sameCoordinates;y++)if(coordinateRows[y]!=tiles.MapRowLookupTable[3*y])sameCoordinates=false;
                    for(int tile=0;tile<pcl.Length&&sameCoordinates;tile++)if(coordinateY[tile]!=tiles.MapColumnLookupTable[tile])sameCoordinates=false;
                    if(sameCoordinates) {result.Rows=coordinateRows;result.X=coordinateX;result.Y=coordinateY;}
                    else
                    {
                        result.Rows=new int[800];result.X=new ushort[pcl.Length];result.Y=new ushort[pcl.Length];
                        for(int y=0;y<800;y++)result.Rows[y]=tiles.MapRowLookupTable[3*y];
                        for(int tile=0;tile<pcl.Length;tile++)
                        {
                            int y=tiles.MapColumnLookupTable[tile];if((uint)y>=800)throw new InvalidOperationException("Packed row outside audited extent");
                            int x=tile-result.Rows[y];if((uint)x>=800)throw new InvalidOperationException("Packed column outside audited extent");
                            result.X[tile]=(ushort)x;result.Y[tile]=(ushort)y;
                        }
                    }
                    for(int tile=0;tile<pcl.Length;tile++)if(result.Pcl[tile]!=0&&result.Anchors[result.Pcl[tile]]<0)result.Anchors[result.Pcl[tile]]=tile;
                    result.SpecialIds=tiles.GetOrganismLayer().ToArray();
                    if(result.SpecialIds.Length!=pcl.Length)throw new InvalidOperationException("Special grid capacity changed");
                    result.SpecialKinds=new short[5000];
                    // Full audited 107160 consumes signed native record IDs, with
                    // kind at native-root + record*0x9C + 0x6A. No public ID lookup.
                    var copiedKinds=new bool[result.SpecialKinds.Length];
                    foreach(ushort raw in result.SpecialIds)
                    {
                        int record=(short)raw;
                        if(record>0&&record<copiedKinds.Length&&!copiedKinds[record])
                        {result.SpecialKinds[record]=unchecked((short)read(0x32DE440+record*0x9C+0x6A));copiedKinds[record]=true;}
                    }
                    result.Records=path.GetPathConnectionRecords().ToArray();
                    result.ConnectionLimit=read(0x60AD660);
                    if(result.ConnectionLimit<1||result.ConnectionLimit>result.Records.Length)throw new InvalidOperationException("Connection high-water exceeds API view");
                    var buildings=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                    result.Owners=new int[buildings.Length];result.Capturers=new int[buildings.Length];result.Globals=new uint[buildings.Length];
                    for(int spanIndex=0;spanIndex<buildings.Length;spanIndex++)if(BridgeSnapshot.Active(buildings[spanIndex].r_AliveState))
                    {result.Owners[spanIndex]=buildings[spanIndex].r_PlayerIdOwner;result.Capturers[spanIndex]=buildings[spanIndex].r_CapturedByPlayerId;result.Globals[spanIndex]=buildings[spanIndex].r_GlobalId;}
                    var units=GameUnitManagerAPI.Instance.GetUnitsAsSpan();result.UnitGlobals=new uint[units.Length];
                    for(int spanIndex=0;spanIndex<units.Length;spanIndex++)if(BridgeSnapshot.Active(units[spanIndex].r_AliveState))result.UnitGlobals[spanIndex]=units[spanIndex].r_GlobalId;
                    result.Allies=new bool[9,9];for(int a=1;a<=8;a++)for(int b=1;b<=8;b++)result.Allies[a,b]=BridgeSnapshot.Allied(a,b);
                    index.Refresh();var decks=new List<Deck>();
                    foreach(var entry in index.Entries)
                    {
                        int spanIndex=BridgeBuildingIndex.SpanIndex(entry.Id,buildings.Length);if(spanIndex<0)continue;
                        ref GameBuilding b=ref buildings[spanIndex];
                        if(b.r_GlobalId!=entry.Global||!BridgeSnapshot.Active(b.r_AliveState))throw new InvalidOperationException("Bridge identity changed during capture");
                        if(b.r_OccupyTileGridSize!=5||b.r_SpriteVariationIndex>7)throw new InvalidOperationException("Deck mapper unknown");
                        var deck=new List<int>();fixed(GameBuilding* ptr=&b)
                        {uint* occupied=&ptr->r_OccupiedTileIdsArrayBegin;for(int i=0;i<25;i++)if(read(0x2D1A30+(b.r_SpriteVariationIndex/2)*100+i*4)!=0)
                        {if(occupied[i]>=pcl.Length)throw new InvalidOperationException("Deck tile outside copied map");deck.Add((int)occupied[i]);}}
                        int parent=BridgeBuildingIndex.SpanIndex(entry.ParentId,buildings.Length);
                        bool authorized=entry.Link=="native-ordered-footprint-coupling"&&parent>=0&&entry.ParentGlobal!=0&&buildings[parent].r_GlobalId==entry.ParentGlobal&&BridgeSnapshot.Active(buildings[parent].r_AliveState)&&BridgeSnapshot.IsGate(buildings[parent].r_BuildingType)&&buildings[parent].r_PlayerIdOwner==b.r_PlayerIdOwner;
                        decks.Add(new Deck {Id=entry.Id,Global=entry.Global,Owner=b.r_PlayerIdOwner,Capturer=b.r_CapturedByPlayerId,NativeParent=b.r_GatehouseId,ParentId=parent>=0?entry.ParentId:0,ParentGlobal=parent>=0?entry.ParentGlobal:0,ParentOwner=parent>=0?buildings[parent].r_PlayerIdOwner:0,ParentCapturer=parent>=0?buildings[parent].r_CapturedByPlayerId:0,Tiles=deck.ToArray(),Authorized=authorized,
                            // Only the documented orientation0 fixture has a closed-boundary comparison.
                            BoundaryVerified=b.r_SpriteVariationIndex==0&&deck.Count==15});
                    }
                    result.Decks=decks.ToArray();
                    if(read(0x60AD6CC)!=dirty||read(0x60AD6D4)!=nativeRevision||session!=result.Session||identity!=result.Identity)return;
                    result.Map=new VirtualBridgeMap(result.Session,result.Revision,result.Pcl,result.Edges,result.Flags,result.X,result.Y,result.Rows,Array.Empty<VirtualConnection>(),true,result.SpecialIds,result.SpecialKinds,takeOwnership:true);
                    if(decisionOnly){result.Token=new object();decisionCaptured=result;return;}
                    long comparisonStart=Stopwatch.GetTimestamp();bool sameContent=SameContent(lastContent,result);comparisonTicks+=Stopwatch.GetTimestamp()-comparisonStart;
                    result.Token=sameContent?lastContent.Token:new object();if(sameContent)contentReused++;lastContent=result;
                    captured=result;coordinateRows=result.Rows;coordinateX=result.X;coordinateY=result.Y;missingInput="none";captures++;
                    emit("virtual-topology","revision="+revision+",nativeRevision="+nativeRevision+",captureClock="+now+",tiles="+pcl.Length+",connections="+result.Records.Length+",bridges="+decks.Count+",contentReusedAfterExactCompare="+sameContent+",coordinatesReusedAfterExactCompare="+sameCoordinates+",input=copied-after-rebuild,seedExceptions=copied-signed-107160-records,macroThirdEndpoints=conditional,negativeCutPolicy=unvalidated,behavior=unchanged");
                }
                catch(Exception error) {emit("virtual-input-unknown","reason="+error.GetType().Name+",message="+error.Message);}
                finally {if(decisionOnly){decisionCaptureTicks+=Stopwatch.GetTimestamp()-start;decisionCaptures++;}else captureTicks+=Stopwatch.GetTimestamp()-start;}
            }
        }
        internal void Compare(string stage,long op,long parent,int player,int from,int to,int mode,int native)
        {
            lock(gate)
            {
                if(session==0||player<1||player>8)return;
                attempted++;
                long validationStart=Stopwatch.GetTimestamp();
                if(captured!=null)
                {
                    bool coherent=read(0x60AD6CC)==0;
                    var currentBuildings=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();var currentUnits=GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                    foreach(var bridge in captured.Decks)
                    {
                        int slot=BridgeBuildingIndex.SpanIndex(bridge.Id,currentBuildings.Length);
                        if(slot<0||!BridgeSnapshot.Active(currentBuildings[slot].r_AliveState)||currentBuildings[slot].r_GlobalId!=bridge.Global||currentBuildings[slot].r_PlayerIdOwner!=bridge.Owner||currentBuildings[slot].r_CapturedByPlayerId!=bridge.Capturer||currentBuildings[slot].r_GatehouseId!=bridge.NativeParent)coherent=false;
                        if(bridge.ParentId!=0)
                        {
                            int parentSlot=BridgeBuildingIndex.SpanIndex(bridge.ParentId,currentBuildings.Length);
                            if(parentSlot<0||currentBuildings[parentSlot].r_AliveState!=AliveState.IsAlive||currentBuildings[parentSlot].r_GlobalId!=bridge.ParentGlobal||currentBuildings[parentSlot].r_PlayerIdOwner!=bridge.ParentOwner||currentBuildings[parentSlot].r_CapturedByPlayerId!=bridge.ParentCapturer)coherent=false;
                        }
                    }
                    for(int recordId=1;recordId<captured.ConnectionLimit&&coherent;recordId++)
                    {
                        int slot=BridgeBuildingIndex.SpanIndex(captured.Records[recordId].r_BuildingId,currentBuildings.Length);
                        if(slot>=0&&(!BridgeSnapshot.Active(currentBuildings[slot].r_AliveState)||currentBuildings[slot].r_GlobalId!=captured.Globals[slot]||currentBuildings[slot].r_PlayerIdOwner!=captured.Owners[slot]||currentBuildings[slot].r_CapturedByPlayerId!=captured.Capturers[slot]))coherent=false;
                        int unitSlot=BridgeBuildingIndex.SpanIndex(captured.Records[recordId].r_UnitId,currentUnits.Length);
                        if(unitSlot>=0&&(!BridgeSnapshot.Active(currentUnits[unitSlot].r_AliveState)||currentUnits[unitSlot].r_GlobalId!=captured.UnitGlobals[unitSlot]))coherent=false;
                    }
                    for(int a=1;a<=8&&coherent;a++)for(int b=1;b<=8;b++)if(captured.Allies[a,b]!=BridgeSnapshot.Allied(a,b)) {coherent=false;break;}
                    if(!coherent) {missingInput=read(0x60AD6CC)!=0?"dirty-topology-at-decision":"identity-role-or-alliance-changed-at-decision";identity++;captured=null;}
                }
                IEnemyGateRoutePolicySnapshot gatePolicy=null;
                var provider=EnemyGatePathPolicyBridge.Current as IEnemyGateRoutePolicyProvider;
                if(provider!=null)provider.TryCaptureRoutePolicy(player,out gatePolicy);
                bool policyValid=gatePolicy==null||gatePolicy.IsCurrent;
                validationTicks+=Stopwatch.GetTimestamp()-validationStart;
                if(stage.StartsWith("group-formation"))
                {
                    backgroundDeferred++;
                    var retained=new Request {QuerySourceOp=op,Stage=stage,Op=op,Parent=parent,Player=player,From=from,To=to,Mode=mode,Native=native,Clock=Stopwatch.GetTimestamp(),Input=captured,InputReason=captured==null?missingInput:"valid-at-decision",GatePolicy=gatePolicy,PolicyValidAtDecision=policyValid};
                    if(mode==0) {if(groups.Count>=32) {retainedGroupEvictions+=groups.Count;groups.Clear();}groups[op]=retained;}
                    else if(groups.TryGetValue(op,out Request primary))primary.Alternative=retained;
                    return; // Promote retained Pre inputs only after stored deck-route evidence.
                }
                var key=new QueryKey(stage,player,from,to,mode,native,revision,identity,gatePolicy,policyValid,captured);
                if((expectedPlanningFamily==0||parent!=expectedPlanningFamily)&&repeats.TryGetValue(key,out Repeat repeat))
                {
                    coalesced++;repeat.Count++;
                    if(repeat.Parent!=parent&&player==8)
                        emit("virtual-shadow-reference","stage="+stage+",op="+op+",parentOp="+parent+",sourceDefinition="+repeat.Id+",sourceQueryOp="+repeat.Op+",player="+player+",revision="+revision+",observationClock="+Stopwatch.GetTimestamp()+",sameNumericInput=True,resultTiming=source-query,behavior=unchanged");
                    repeat.Parent=parent;repeat.LastOp=op;repeats[key]=repeat;return;
                }
                if(repeats.Count>4096) {FlushRepeats();repeats.Clear();}
                if(pending.Count>=32&&(expectedPlanningFamily==0||parent!=expectedPlanningFamily)) {rejected++;if(rejected<=8)emit("virtual-shadow-rejected","op="+op+",player="+player+",stage="+stage+",reason=selected-queue-full");return;}
                long definition=++queryId;repeats[key]=new Repeat {Id=definition,Op=op,Parent=parent,Count=1,LastOp=op};
                var request=new Request {Id=definition,QuerySourceOp=op,Stage=stage,Op=op,Parent=parent,Player=player,From=from,To=to,Mode=mode,Native=native,Clock=Stopwatch.GetTimestamp(),Input=captured,InputReason=captured==null?missingInput:"valid-at-decision",GatePolicy=gatePolicy,PolicyValidAtDecision=policyValid};
                emit("virtual-shadow-input","definition="+definition+",stage="+stage+",op="+op+",parentOp="+parent+",player="+player+",fromTile="+from+",toTile="+to+",mode="+mode+",observationClock="+request.Clock+",sourceSession="+(captured?.Session??0)+",captureClock="+(captured?.Clock??0)+",revision="+(captured?.Revision??-1)+",captureAvailable="+(captured!=null)+",inputReason="+request.InputReason+",policyValidAtDecision="+request.PolicyValidAtDecision);
                request.Scheduled=true;
                if(player==8&&stage=="keep-access"&&!keepArtifact&&parent==decisionCaptureOp)
                {planningRequest=request;}
                if(stage=="keep-access")pending.AddFirst(request);else pending.AddLast(request);
            }
        }
        internal void CompareXY(string stage,long op,long parent,int player,int x,int y,int targetX,int targetY)
        {
            lock(gate)
            {
                var input=captured;
                int from=input==null?-1:Tile(input,x,y),to=input==null?-1:Tile(input,targetX,targetY);
                // Event exposes no audited group mode. Report both hypotheses explicitly.
                Compare(stage+"-mode-hypothesis",op,parent,player,from,to,0,-1);
                Compare(stage+"-mode-hypothesis",op,parent,player,from,to,1,-1);
                if(groups.TryGetValue(op,out Request group)){group.XYKnown=true;group.FromX=x;group.FromY=y;group.ToX=targetX;group.ToY=targetY;if(group.Alternative!=null){group.Alternative.XYKnown=true;group.Alternative.FromX=x;group.Alternative.FromY=y;group.Alternative.ToX=targetX;group.Alternative.ToY=targetY;}}
            }
        }
        private static int Tile(Captured input,int x,int y)
        {if((uint)x>=800||(uint)y>=800)return -1;long tile=(long)input.Rows[y]+x;return tile>=0&&tile<input.Pcl.Length&&input.X[tile]==x&&input.Y[tile]==y?(int)tile:-1;}
        // Publication changes do not invalidate an immutable historical input.
        // The live checks were made once, at the decision, on the simulation thread.
        private bool Current(Request request) => request.Input!=null&&request.Input.Session==session&&request.PolicyValidAtDecision;
        internal void GroupContext(long op,int tribe,uint global,long root,long decision,int phase,long decisionRoot=0)
        {
            lock(gate)if(groups.TryGetValue(op,out Request request))
            {request.Tribe=tribe;request.TribeGlobal=global;request.PlanningRoot=root;request.Decision=decision;request.DecisionRoot=decisionRoot;request.Phase=phase;if(request.Alternative!=null) {request.Alternative.Tribe=tribe;request.Alternative.TribeGlobal=global;request.Alternative.PlanningRoot=root;request.Alternative.Decision=decision;request.Alternative.DecisionRoot=decisionRoot;request.Alternative.Phase=phase;}}
        }
        internal void BridgeGroup(long groupOp,int player)
        {
            lock(gate)
            {
                if(player<1||player>8)return;
                groups.TryGetValue(groupOp,out Request request);
                if(request!=null&&request.Input==null&&player==8&&keepArtifact&&!groupArtifact&&decisionIndex!=null&&(request.PlanningRoot==planningBundleOp||request.DecisionRoot==planningBundleOp))
                {
                    var planInput=decisionCaptured;decisionCaptured=null;Rebuilt(session,revision,decisionIndex,true);var groupInput=decisionCaptured;decisionCaptured=planInput;
                    if(groupInput!=null){request.Input=groupInput;request.InputReason="synchronous-stored-path-observation-not-command-pre";if(request.XYKnown){request.From=Tile(groupInput,request.FromX,request.FromY);request.To=Tile(groupInput,request.ToX,request.ToY);}if(request.Alternative!=null){request.Alternative.Input=groupInput;request.Alternative.InputReason=request.InputReason;request.Alternative.From=request.From;request.Alternative.To=request.To;}}
                }
                if(request==null||request.Input==null||!request.PolicyValidAtDecision)
                {unresolvedGroupLinks++;if(unresolvedGroupLinks<=8)emit("virtual-group-unresolved","op="+groupOp+",player="+player+",reason="+(request==null?"no-retained-group-pre":request.Input==null?request.InputReason:"policy-stale-at-decision"));return;}
                request.BridgeRelevant=true;
                if(!Promote(request))return;
                if(request.Alternative!=null)Promote(request.Alternative);
                if(player==8&&!groupArtifact&&keepArtifact&&planningBundleOp!=0&&(request.PlanningRoot==planningBundleOp||request.DecisionRoot==planningBundleOp))groupArtifact=artifacts.Enqueue(request.Input.Session,request.Id,Artifact(request),2);
            }
        }
        private bool Promote(Request request)
        {
            if(request.Scheduled)return true;
            if(request.Input.Dirty!=0){request.Id=++queryId;request.Scheduled=true;emit("virtual-shadow-deferred","definition="+request.Id+",op="+request.Op+",reason=pending-topology-at-own-capture,dirty="+request.Input.Dirty+",captureClock="+request.Input.Clock+",negativeEligible=False,artifactOnly=True,behavior=unchanged");return true;}
            var key=new QueryKey(request.Stage,request.Player,request.From,request.To,request.Mode,request.Native,request.Input.Revision,request.Input.Identity,request.GatePolicy,request.PolicyValidAtDecision,request.Input);
            if(repeats.TryGetValue(key,out Repeat repeat))
            {
                coalesced++;repeat.Count++;repeat.Parent=request.Parent;repeat.LastOp=request.Op;repeats[key]=repeat;
                request.Id=repeat.Id;request.QuerySourceOp=repeat.Op;request.Scheduled=true;
                emit("virtual-shadow-reference","stage="+request.Stage+",op="+request.Op+",parentOp="+request.Parent+",sourceDefinition="+repeat.Id+",sourceQueryOp="+repeat.Op+",player="+request.Player+",planningRoot="+request.PlanningRoot+",decision="+request.Decision+",phase="+request.Phase+",tribe="+request.Tribe+",tribeGlobal="+request.TribeGlobal+",captureClock="+request.Input.Clock+",observationClock="+request.Clock+",sameNumericInput=True,resultTiming=source-query,behavior=unchanged");return true;
            }
            if(pending.Count>=32) {rejected++;if(rejected<=8)emit("virtual-shadow-rejected","op="+request.Op+",player="+request.Player+",stage="+request.Stage+",reason=selected-queue-full");return false;}
            if(repeats.Count>4096) {FlushRepeats();repeats.Clear();}
            request.Id=++queryId;request.Scheduled=true;
            repeats[key]=new Repeat {Id=request.Id,Op=request.Op,Parent=request.Parent,Count=1,LastOp=request.Op};
            emit("virtual-shadow-input","definition="+request.Id+",stage="+request.Stage+",op="+request.Op+",parentOp="+request.Parent+",player="+request.Player+",fromTile="+request.From+",toTile="+request.To+",mode="+request.Mode+",observationClock="+request.Clock+",sourceSession="+request.Input.Session+",captureClock="+request.Input.Clock+",revision="+request.Input.Revision+",captureAvailable=True,inputReason="+request.InputReason+",policyValidAtDecision="+request.PolicyValidAtDecision+",selection=stored-deck-route,planningRoot="+request.PlanningRoot+",decision="+request.Decision+",phase="+request.Phase+",tribe="+request.Tribe+",tribeGlobal="+request.TribeGlobal);
            pending.AddLast(request);return true;
        }
        private void Prepare(Request request)
        {
            var input=request.Input;
            var preparationKey=new QueryKey("prepared",request.Player,0,0,0,0,input.Revision,input.Identity,request.GatePolicy,request.PolicyValidAtDecision,input);
            if(!preparations.TryGetValue(preparationKey,out Preparation[] variants))
            {if(preparations.Count>=8)preparations.Clear();preparations[preparationKey]=variants=new Preparation[3];}
            int variant=request.Variant%3;
            if(variants[variant]!=null) {preparationHits++;UsePreparation(request,variants[variant]);return;}
            var deck=new List<int>();var affected=new HashSet<int>();bool authorized=true,boundary=true;
            foreach(var bridge in input.Decks)
            {
                if(request.Variant%3==0||request.Variant%3==1&&bridge.Id!=703)continue;
                if(bridge.Owner<1||bridge.Owner>8||bridge.Capturer<0||bridge.Capturer>8) {authorized=false;continue;}
                if(input.Allies[request.Player,bridge.Owner]||bridge.Capturer!=0&&input.Allies[request.Player,bridge.Capturer])continue;
                // Only confirmed copied coupling supplies Gate ownership/capture permission.
                if(bridge.Authorized&&bridge.ParentId>0)
                {
                    if(bridge.ParentOwner<1||bridge.ParentOwner>8||bridge.ParentCapturer<0||bridge.ParentCapturer>8)authorized=false;
                    else if(input.Allies[request.Player,bridge.ParentOwner]||bridge.ParentCapturer!=0&&input.Allies[request.Player,bridge.ParentCapturer])continue;
                }
                bool open=false;foreach(int tile in bridge.Tiles)if((input.Flags[tile]&0x40000000)==0)open=true;
                if(open) {authorized&=bridge.Authorized;boundary&=bridge.BoundaryVerified;}
                foreach(int tile in bridge.Tiles) {if((input.Flags[tile]&0x40000000)!=0)continue;deck.Add(tile);if(input.Pcl[tile]!=0)affected.Add(input.Pcl[tile]);}
            }
            var connections=new List<VirtualConnection>();var componentLinks=new List<VirtualComponentLink>();
            request.UnknownRecords=0;
            for(int recordId=1;recordId<input.ConnectionLimit;recordId++)
            {
                var r=input.Records[recordId];int buildingSlot=BridgeBuildingIndex.SpanIndex(r.r_BuildingId,input.Owners.Length);
                bool nativeAccess=(r.r_OwnerOrAccessPlayerId>0&&r.r_OwnerOrAccessPlayerId<9&&input.Allies[request.Player,r.r_OwnerOrAccessPlayerId])||buildingSlot>=0&&input.Capturers[buildingSlot]!=0;
                componentLinks.Add(new VirtualComponentLink(r.r_PathComponentA,r.r_PathComponentB,r.r_PathComponentC,(int)r.r_ConnectionClass,r.r_IsActive==1&&r.r_IsEnabledOrOpen!=0&&nativeAccess));
                if(buildingSlot>=0&&input.Owners[buildingSlot]>0&&input.Owners[buildingSlot]<9)
                    nativeAccess&=input.Allies[request.Player,input.Owners[buildingSlot]]||input.Capturers[buildingSlot]>0&&input.Capturers[buildingSlot]<9&&input.Allies[request.Player,input.Capturers[buildingSlot]];
                bool known=NativeEndpoint(input,r.r_EntryTileId,r.r_PathComponentA)&&NativeEndpoint(input,r.r_ExitTileId,r.r_PathComponentB);
                if(r.r_BuildingId!=0)known&=buildingSlot>=0&&input.Globals[buildingSlot]!=0&&input.Globals[buildingSlot]==r.r_SubjectGlobalId;
                int unitSlot=BridgeBuildingIndex.SpanIndex(r.r_UnitId,input.UnitGlobals.Length);
                if(r.r_UnitId!=0)known&=unitSlot>=0&&input.UnitGlobals[unitSlot]!=0&&input.UnitGlobals[unitSlot]==r.r_SubjectGlobalId;
                int third=-1;
                bool thirdUnknown=r.r_PathComponentC>0; // Unknown classes retain independently known A/B.
                if(known&&thirdUnknown&&VirtualGateEndpoint.TryResolve(input.Map,(int)r.r_ConnectionClass,r.r_EntryTileId,r.r_ExitTileId,r.r_PathComponentC,out third))thirdUnknown=false;
                if(r.r_IsActive==1&&r.r_IsEnabledOrOpen!=0&&nativeAccess&&(!known||thirdUnknown))
                {
                    request.UnknownRecords++;
                    if(request.Variant==0&&request.UnknownRecords<=2)emit("virtual-connection-unknown","op="+request.Op+",recordId="+recordId+",captureClock="+input.Clock+",recordGlobal="+r.r_RecordGlobalId+",subjectGlobal="+r.r_SubjectGlobalId+",buildingId="+r.r_BuildingId+",unitId="+r.r_UnitId+",class="+(int)r.r_ConnectionClass+",entry="+r.r_EntryTileId+",exit="+r.r_ExitTileId+",components="+r.r_PathComponentA+"/"+r.r_PathComponentB+"/"+r.r_PathComponentC+",thirdAffected="+affected.Contains(r.r_PathComponentC)+",knownAB="+known+",thirdEndpointUnknown="+thirdUnknown+",reason=partial-endpoint-or-subject-global-unresolved");
                }
                connections.Add(new VirtualConnection(r.r_EntryTileId,r.r_ExitTileId,third,(int)r.r_ConnectionClass,r.r_IsActive==1,r.r_IsEnabledOrOpen!=0,nativeAccess,known,thirdUnknown));
            }
            var prepared=new Preparation {Map=input.Map.WithConnections(connections.ToArray()),Deck=deck.ToArray(),Links=componentLinks.ToArray(),Unknown=request.UnknownRecords,Authorized=authorized,Boundary=boundary};
            variants[variant]=prepared;UsePreparation(request,prepared);
        }
        private void UsePreparation(Request request,Preparation prepared)
        {
            request.UnknownRecords=prepared.Unknown;request.DeckCells=prepared.Deck.Length;
            request.Authorization=prepared.Authorized&&prepared.Boundary&&prepared.Deck.Length==0&&request.GatePolicy!=null&&!request.Stage.Contains("hypothesis");
            if(request.Variant==0&&request.Stage=="keep-access"&&(uint)request.From<(uint)request.Input.Pcl.Length&&(uint)request.To<(uint)request.Input.Pcl.Length)
                request.Macro=VirtualComponentControl.Evaluate(request.Input.Pcl[request.To],request.Input.Pcl[request.From],request.Mode,prepared.Links);
            request.Cuts[request.Variant]=prepared.Deck.Length;
            int from=request.Variant<3?request.From:request.To,to=request.Variant<3?request.To:request.From;
            if(workspace==null||workspace.Cut.Length!=prepared.Map.Components.Length)workspace=new VirtualBridgeWorkspace(prepared.Map.Components.Length);
            request.Query=new VirtualBridgeQuery(prepared.Map,from,to,request.Mode,prepared.Deck,true,
                request.GatePolicy==null?(Func<int,int,bool>)null:request.GatePolicy.IsDirectionAllowed,negativeProofComplete:true,workspace:workspace);
        }
        private static bool EqualArray<T>(T[] a,T[] b)
        {if(ReferenceEquals(a,b))return true;if(a==null||b==null||a.Length!=b.Length)return false;var comparer=EqualityComparer<T>.Default;for(int i=0;i<a.Length;i++)if(!comparer.Equals(a[i],b[i]))return false;return true;}
        private static bool SameContent(Captured a,Captured b)
        {
            if(a==null||a.Session!=b.Session||a.ConnectionLimit!=b.ConnectionLimit||a.Decks.Length!=b.Decks.Length)return false;
            if(!EqualArray(a.Pcl,b.Pcl)||!EqualArray(a.Edges,b.Edges)||!EqualArray(a.Flags,b.Flags)||!EqualArray(a.X,b.X)||!EqualArray(a.Y,b.Y)||!EqualArray(a.Rows,b.Rows)||!EqualArray(a.SpecialIds,b.SpecialIds)||!EqualArray(a.SpecialKinds,b.SpecialKinds)||!EqualArray(a.Owners,b.Owners)||!EqualArray(a.Capturers,b.Capturers)||!EqualArray(a.Globals,b.Globals)||!EqualArray(a.UnitGlobals,b.UnitGlobals))return false;
            for(int i=0;i<9;i++)for(int j=0;j<9;j++)if(a.Allies[i,j]!=b.Allies[i,j])return false;
            for(int i=1;i<a.ConnectionLimit;i++)
            {
                var x=a.Records[i];var y=b.Records[i];
                if(x.r_IsActive!=y.r_IsActive||x.r_ConnectionClass!=y.r_ConnectionClass||x.r_RecordGlobalId!=y.r_RecordGlobalId||x.r_BuildingId!=y.r_BuildingId||x.r_UnitId!=y.r_UnitId||x.r_SubjectGlobalId!=y.r_SubjectGlobalId||x.r_IsEnabledOrOpen!=y.r_IsEnabledOrOpen||x.r_EntryTileId!=y.r_EntryTileId||x.r_ExitTileId!=y.r_ExitTileId||x.r_PathComponentA!=y.r_PathComponentA||x.r_PathComponentB!=y.r_PathComponentB||x.r_OwnerOrAccessPlayerId!=y.r_OwnerOrAccessPlayerId||x.r_PathComponentC!=y.r_PathComponentC)return false;
            }
            for(int i=0;i<a.Decks.Length;i++)
            {var x=a.Decks[i];var y=b.Decks[i];if(x.Id!=y.Id||x.Global!=y.Global||x.Owner!=y.Owner||x.Capturer!=y.Capturer||x.NativeParent!=y.NativeParent||x.ParentId!=y.ParentId||x.ParentGlobal!=y.ParentGlobal||x.ParentOwner!=y.ParentOwner||x.ParentCapturer!=y.ParentCapturer||x.Authorized!=y.Authorized||x.BoundaryVerified!=y.BoundaryVerified||!EqualArray(x.Tiles,y.Tiles))return false;}
            return true;
        }
        private static bool NativeEndpoint(Captured input,int tile,int component) => (uint)tile<(uint)input.Pcl.Length&&component>0&&input.Pcl[tile]==component;
        internal void Pump()
        {
            lock(gate)
            {
                long start=Stopwatch.GetTimestamp();
                try
                {
                    while((Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency<1)
                    {
                        if(active==null) {if(pending.Count==0)break;active=pending.First.Value;pending.RemoveFirst();}
                        if(!Current(active)) {Deliver("Unknown","Unknown","missing-decision-input-or-policy-"+(active.Input==null?active.InputReason:"stale-policy-at-decision"),0,false);active=null;continue;}
                        if(active.Query==null) {long prepareStart=Stopwatch.GetTimestamp();try {Prepare(active);}finally {prepareTicks+=Stopwatch.GetTimestamp()-prepareStart;}}
                        long searchStart=Stopwatch.GetTimestamp();try {active.Query.Step(64);}finally {searchTicks+=Stopwatch.GetTimestamp()-searchStart;}
                        if(!active.Query.Complete)continue;
                        active.Results[active.Variant]=active.Query.Result;active.Reasons[active.Variant]=active.Query.Reason;active.Expansions[active.Variant]=active.Query.Expanded;
                        if(++active.Variant<6) {active.Query=null;continue;}
                        Deliver(active.Results[2].ToString(),"Unknown","controlled-six-variant-comparison",Sum(active.Expansions),false);active=null;
                    }
                }
                catch(Exception error)
                {
                    if(active!=null)Deliver("Unknown","Unknown","calculation-error-"+error.GetType().Name,active.Query?.Expanded??0,false);
                    else emit("virtual-shadow-error","reason="+error.GetType().Name);
                    active=null;
                }
                finally {computeTicks+=Stopwatch.GetTimestamp()-start;}
            }
        }
        private IEnumerable<byte[]> Artifact(Request request,BridgePlanningCapture.Bundle planning=null)
        {return ArtifactCore(request.ArtifactCopy(),planning,planningBundleDefinition,planningBundleOp);}
        private IEnumerable<byte[]> ArtifactCore(Request request,BridgePlanningCapture.Bundle planning,long bundleDefinition,long bundleRoot)
        {
            var input=request.Input;
            yield return BridgeInputArtifact.Header((planning==null?"planningBundleDefinition="+bundleDefinition+"\nplanningBundleRoot="+bundleRoot+"\nplanningAssociation="+(request.PlanningRoot==bundleRoot?"same-parent-root":request.DecisionRoot==bundleRoot?"retained-decision-root":"unresolved")+"\n":planning.Metadata+"\n")+"nativeHash="+BridgeNativeDefinition.NativeHash+"\nbackendHash="+BridgeNativeDefinition.BackendHash+"\nsession="+input.Session+"\nrevision="+input.Revision+"\nnativeRevision="+input.NativeRevision+"\ndirty="+input.Dirty+"\nnegativeEligible=False\ninputTiming="+request.InputReason+"\nidentity="+input.Identity+"\ncaptureClock="+input.Clock+"\nobservationClock="+request.Clock+"\ndefinition="+request.Id+"\nop="+request.Op+"\nquerySourceOp="+request.QuerySourceOp+"\nparent="+request.Parent+"\ntribe="+request.Tribe+"\ntribeGlobal="+request.TribeGlobal+"\nplanningRoot="+request.PlanningRoot+"\ndecision="+request.Decision+"\ndecisionRoot="+request.DecisionRoot+"\nphase="+request.Phase+"\nstage="+request.Stage+"\nplayer="+request.Player+"\nfrom="+request.From+"\nto="+request.To+"\nmode="+request.Mode+"\nnativeBoolean="+request.Native+"\nconnectionLimit="+input.ConnectionLimit+"\npolicyValidAtDecision="+request.PolicyValidAtDecision+"\nnativeDirection="+(request.Stage=="keep-access"?"target-to-attacker":"not-observed")+"\nmovementDirection=attacker-to-target\nvariants=none,only703,all-hostile\ngateFilter="+(request.GatePolicy==null?"absent":"immutable-player-direction-mask"),2);
            if(planning!=null)foreach(var block in planning.Serialize())yield return block;
            foreach(var block in BridgeInputArtifact.Section("components",input.Pcl,2))yield return block;
            foreach(var block in BridgeInputArtifact.Section("edges",input.Edges,1))yield return block;
            foreach(var block in BridgeInputArtifact.Section("flags",input.Flags,4))yield return block;
            foreach(var block in BridgeInputArtifact.Section("x",input.X,2))yield return block;
            foreach(var block in BridgeInputArtifact.Section("y",input.Y,2))yield return block;
            foreach(var block in BridgeInputArtifact.Section("rows",input.Rows,4))yield return block;
            foreach(var block in BridgeInputArtifact.Section("specialIds",input.SpecialIds,2))yield return block;
            foreach(var block in BridgeInputArtifact.Section("specialKinds",input.SpecialKinds,2))yield return block;
            foreach(var block in BridgeInputArtifact.Section("owners",input.Owners,4))yield return block;
            foreach(var block in BridgeInputArtifact.Section("capturers",input.Capturers,4))yield return block;
            foreach(var block in BridgeInputArtifact.Section("globals",input.Globals,4))yield return block;
            foreach(var block in BridgeInputArtifact.Section("unitGlobals",input.UnitGlobals,4))yield return block;
            var allies=new byte[81];for(int a=0;a<9;a++)for(int b=0;b<9;b++)allies[a*9+b]=(byte)(input.Allies[a,b]?1:0);
            foreach(var block in BridgeInputArtifact.Section("allies",allies,1))yield return block;
            // All consumed public record members, including raw component C (no guessed endpoint).
            var records=new int[input.Records.Length*13];for(int i=0;i<input.Records.Length;i++)
            {
                var r=input.Records[i];int p=i*13;
                records[p]=r.r_IsActive;records[p+1]=(int)r.r_ConnectionClass;records[p+2]=unchecked((int)r.r_RecordGlobalId);
                records[p+3]=r.r_BuildingId;records[p+4]=r.r_UnitId;records[p+5]=unchecked((int)r.r_SubjectGlobalId);records[p+6]=r.r_IsEnabledOrOpen;
                records[p+7]=r.r_EntryTileId;records[p+8]=r.r_ExitTileId;records[p+9]=r.r_PathComponentA;records[p+10]=r.r_PathComponentB;records[p+11]=r.r_OwnerOrAccessPlayerId;records[p+12]=r.r_PathComponentC;
            }
            foreach(var block in BridgeInputArtifact.Section("records13",records,4))yield return block;
            var decks=new List<int>();foreach(var d in input.Decks)
            {decks.Add(d.Id);decks.Add(unchecked((int)d.Global));decks.Add(d.Owner);decks.Add(d.Capturer);decks.Add(d.NativeParent);decks.Add(d.Authorized?1:0);decks.Add(d.BoundaryVerified?1:0);decks.Add(d.Tiles.Length);decks.AddRange(d.Tiles);}
            foreach(var block in BridgeInputArtifact.Section("decks",decks.ToArray(),4))yield return block;
            // Optional schema1 section: old complete captures remain replayable.
            var parents=new List<int>();foreach(var d in input.Decks)
            {parents.Add(d.Id);parents.Add(d.ParentId);parents.Add(unchecked((int)d.ParentGlobal));parents.Add(d.ParentOwner);parents.Add(d.ParentCapturer);}
            foreach(var block in BridgeInputArtifact.Section("coupledParents5",parents.ToArray(),4))yield return block;
            // Mask generation is lazy in small pieces; snapshot getter is copied managed data.
            foreach(var block in BridgeInputArtifact.Section("gateMaskHeader",new[]{input.Pcl.Length,request.GatePolicy==null?0:1},4))yield return block;
            for(int start=0;start<input.Pcl.Length;start+=512)
            {
                var mask=new byte[Math.Min(512,input.Pcl.Length-start)];for(int i=0;i<mask.Length;i++)
                    for(int direction=0;direction<8;direction++)if(request.GatePolicy==null||request.GatePolicy.IsDirectionAllowed(start+i,direction))mask[i]|=(byte)(1<<direction);
                foreach(var block in BridgeInputArtifact.Section("gateMaskChunk",mask,1))yield return block;
            }
        }
        private void FlushRepeats()
        {
            var rows=new System.Text.StringBuilder();int count=0;
            foreach(var key in new List<QueryKey>(repeats.Keys))
            {
                var value=repeats[key];if(value.Count<=1)continue;
                rows.Append(value.Id).Append('/').Append(value.Count-1).Append('/').Append(value.Op).Append('/').Append(value.LastOp).Append('/').Append(value.Parent).Append(';');
                value.Count=1;repeats[key]=value;
                if(++count==32) {emit("virtual-shadow-repeat-batch","columns=definition/repeatCount/sourceOp/lastOp/lastParent,intermediateParentCoverage=summarized,rows=["+rows+"]");rows.Clear();count=0;}
            }
            if(count!=0)emit("virtual-shadow-repeat-batch","columns=definition/repeatCount/sourceOp/lastOp/lastParent,intermediateParentCoverage=summarized,rows=["+rows+"]");
        }
        internal void FlushCosts()
        {
            lock(gate)
            {
                long now=Stopwatch.GetTimestamp();if(costClock==0) {costClock=now;return;}
                double seconds=(now-costClock)/(double)Stopwatch.Frequency;if(seconds<10)return;
                FlushRepeats();
                emit("interval-virtual-cost","decisionInputCopies="+(decisionCaptures-lastDecisionCaptures)+",decisionInputCopyMs="+Ms(decisionCaptureTicks-lastDecisionCaptureTicks)+",seconds="+seconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+",captures="+(captures-lastCaptures)+",captureMs="+Ms(captureTicks-lastCaptureTicks)+",computeMs="+Ms(computeTicks-lastComputeTicks)+",prepareMs="+Ms(prepareTicks-lastPrepareTicks)+",searchMs="+Ms(searchTicks-lastSearchTicks)+",formatMs="+Ms(formatTicks-lastFormatTicks)+",validationMs="+Ms(validationTicks-lastValidationTicks)+",contentCompareMs="+Ms(comparisonTicks-lastComparisonTicks)+",preparationHits="+(preparationHits-lastPreparationHits)+",contentReused="+(contentReused-lastContentReused)+",backgroundDeferred="+(backgroundDeferred-lastDeferred)+",coalesced="+(coalesced-lastCoalesced)+",rejected="+(rejected-lastRejected)+",pending="+(pending.Count+(active==null?0:1))+",activeRevision="+revision+",retainedSnapshot="+(captured!=null)+",artifactBytes="+(artifacts.Bytes-lastArtifactBytes)+",formatScope=result-control-records,computeIncludesPrepareSearchFormat=True,captureIncludesContentCompare=True,artifactWriteMs="+Ms(artifacts.Ticks-lastArtifactTicks));
                costClock=now;lastDecisionCaptures=decisionCaptures;lastDecisionCaptureTicks=decisionCaptureTicks;lastValidationTicks=validationTicks;lastPrepareTicks=prepareTicks;lastSearchTicks=searchTicks;lastFormatTicks=formatTicks;lastComparisonTicks=comparisonTicks;lastPreparationHits=preparationHits;lastContentReused=contentReused;lastDeferred=backgroundDeferred;lastCaptures=captures;lastCaptureTicks=captureTicks;lastComputeTicks=computeTicks;lastCoalesced=coalesced;lastRejected=rejected;lastArtifactBytes=artifacts.Bytes;lastArtifactTicks=artifacts.Ticks;
            }
        }
        private static long Sum(long[] values) {long sum=0;foreach(long value in values)sum+=value;return sum;}
        private void Deliver(string hypothesis,string policy,string reason,long expanded,bool structure)
        {
            long formatStart=Stopwatch.GetTimestamp();try {DeliverCore(hypothesis,policy,reason,expanded,structure);}finally {formatTicks+=Stopwatch.GetTimestamp()-formatStart;}
        }
        private void DeliverCore(string hypothesis,string policy,string reason,long expanded,bool structure)
        {
            completedQueries++;
            if(active.Variant==6)
            {
                bool observed=active.Stage=="keep-access"&&active.Native>=0;
                bool macroMatch=observed&&active.Macro!=VirtualReachability.Unknown&&(active.Macro==VirtualReachability.Reachable)==(active.Native!=0);
                bool geometryMatch=observed&&active.Results[3]!=VirtualReachability.Unknown&&(active.Results[3]==VirtualReachability.Reachable)==(active.Native!=0);
                for(int direction=0;direction<2;direction++)
                {
                    int b=direction*3;bool baseline=active.Results[b]==VirtualReachability.Reachable;
                    emit("virtual-control","definition="+active.Id+",op="+active.Op+",parentOp="+active.Parent+",player="+active.Player+",direction="+(direction==0?"attacker-to-target":"target-to-attacker")+",mode="+active.Mode+",modeEvidence="+(observed?"observed-CF-mode":"hypothesis")+",macroBasis=raw-native-components,geometryBasis=copied-directed-tiles-with-optional-Gate-filter,noCut="+active.Results[b]+",only703="+active.Results[b+1]+",allHostile="+active.Results[b+2]+",cutCells="+active.Cuts[b]+"/"+active.Cuts[b+1]+"/"+active.Cuts[b+2]+",reasons=["+active.Reasons[b]+";"+active.Reasons[b+1]+";"+active.Reasons[b+2]+"],macroControl="+active.Macro+",nativeBoolean="+active.Native+",nativeDirection="+(observed?"target-to-attacker":"not-observed")+",macroMatchesNative="+macroMatch+",geometryMatchesNative="+geometryMatch+",cutAssessment="+(observed?macroMatch&&geometryMatch&&baseline?"conditional-baseline-agrees":"blocked-baseline-mismatch-or-unknown":baseline?"geometric-hypothesis-only":"blocked-geometric-baseline")+",historicalInput=True,captureClock="+active.Input.Clock+",revision="+active.Input.Revision+",policyResult=Unknown,behavior=unchanged");
                }
            }
            emit("virtual-shadow","definition="+active.Id+",stage="+active.Stage+",op="+active.Op+",parentOp="+active.Parent+",player="+active.Player+",fromTile="+active.From+",toTile="+active.To+",nativeQueryOrder="+(active.Stage=="keep-access"?"keep-target-to-attacker":"not-observed")+",comparisonDirection=attacker-to-target,mode="+active.Mode+",observedOriginalReturn="+active.Native+",observationClock="+active.Clock+",sourceSession="+(active.Input?.Session??0)+",captureClock="+(active.Input?.Clock??0)+",revision="+(active.Input?.Revision??-1)+",geometricResult="+hypothesis+",policyResult="+policy+",policyEvidence="+(active.Authorization?"copied-baseline":"authorization-boundary-mode-or-gate-policy-unvalidated")+",historicalInput=True,currentPublicationRevision="+revision+",inputReason="+active.InputReason+",decisionInputMissing="+(active.Input==null)+",policyValidAtDecision="+active.PolicyValidAtDecision+",reason="+reason+",candidateCutCells="+(active.Variant==6?active.Cuts[2]:active.DeckCells)+",unknownRecords="+active.UnknownRecords+",expanded="+expanded+",structureRequired="+structure+",behavior=unchanged");
        }
    }
}
